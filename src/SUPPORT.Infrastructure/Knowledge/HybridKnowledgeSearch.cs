using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Infrastructure.AI;
using SUPPORT.Infrastructure.Persistence;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>
/// Vector (HNSW, cosine) and full-text candidates fused with Reciprocal Rank Fusion, plus a small boost for the
/// module of the screen the user is on.
/// </summary>
/// <param name="db">Scoped context (only its connection is used).</param>
/// <param name="embeddings">Embedding service for the question.</param>
internal sealed class HybridKnowledgeSearch(SupportDbContext db, EmbeddingService embeddings) : IKnowledgeSearch
{
    /// <summary>Candidates taken from each retriever before fusion.</summary>
    private const int CandidatePool = 20;

    /// <summary>Standard RRF damping constant: rank r contributes 1 / (k + r).</summary>
    private const int RrfK = 60;

    /// <summary>Same-module boost — about one rank step, so it breaks ties without overriding relevance.</summary>
    private const double ModuleBoost = 0.005;

    // Raw ADO rather than LINQ: two CTEs + a fused score are clearer in SQL, and the query vector goes in as text
    // cast to ::vector so no provider type mapping is involved.
    //
    // Full-text runs twice on purpose:
    //  - $10 (words OR-ed) gathers candidates: a natural question ("… thì … đi đâu?") never has ALL its words in one
    //    chunk, so AND would find nothing, while OR still ranks chunks by how many words they share.
    //  - $4 (the question as typed, AND semantics) decides fts_hit, which the "no documentation" guard trusts. OR would
    //    make that flag true for any chunk containing "là" or "thì"; AND only fires on exact terms like a button name.
    private const string Sql = """
        WITH vec AS (
            SELECT id, 1 - (embedding <=> $1::vector) AS cos_sim,
                   row_number() OVER (ORDER BY embedding <=> $1::vector) AS rnk
            FROM kb_chunk
            WHERE product = $2 AND (org_id IS NULL OR org_id = $3)
            ORDER BY embedding <=> $1::vector
            LIMIT $5
        ),
        fts AS (
            SELECT c.id, row_number() OVER (ORDER BY ts_rank_cd(c.search_tsv, q) DESC) AS rnk
            FROM kb_chunk c, websearch_to_tsquery('simple', support_unaccent($10)) q
            WHERE c.product = $2 AND (c.org_id IS NULL OR c.org_id = $3) AND c.search_tsv @@ q
            ORDER BY ts_rank_cd(c.search_tsv, q) DESC
            LIMIT $5
        )
        SELECT c.id, d.title, c.heading_path, c.content, d.route_hint, v.cos_sim,
               c.search_tsv @@ websearch_to_tsquery('simple', support_unaccent($4)) AS fts_hit,
               COALESCE(1.0 / ($7 + v.rnk), 0) + COALESCE(1.0 / ($7 + f.rnk), 0)
             + CASE WHEN c.module = $6 THEN $8 ELSE 0 END AS score
        FROM (SELECT id FROM vec UNION SELECT id FROM fts) ids
        JOIN kb_chunk c    ON c.id = ids.id
        JOIN kb_document d ON d.id = c.document_id AND d.status = 1
        LEFT JOIN vec v ON v.id = c.id
        LEFT JOIN fts f ON f.id = c.id
        ORDER BY score DESC
        LIMIT $9
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct)
    {
        var vector = await embeddings.EmbedOneAsync(query.Text, ct);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);

        try
        {
            await using var command = new NpgsqlCommand(Sql, connection)
            {
                Parameters =
                {
                    new() { Value = ToVectorLiteral(vector) },
                    new() { Value = query.Product },
                    new() { Value = query.OrgId },
                    new() { Value = query.Text },
                    new() { Value = CandidatePool },
                    new() { Value = query.Module ?? "" },
                    new() { Value = (double)RrfK },
                    new() { Value = ModuleBoost },
                    new() { Value = query.TopK },
                    new() { Value = AnyWordQuery(query.Text) },
                },
            };

            var hits = new List<KnowledgeHit>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                hits.Add(new KnowledgeHit(
                    ChunkId: reader.GetGuid(0),
                    Title: reader.GetString(1),
                    HeadingPath: reader.GetString(2),
                    Content: reader.GetString(3),
                    RouteHint: reader.IsDBNull(4) ? null : reader.GetString(4),
                    CosineSimilarity: reader.IsDBNull(5) ? null : (float)reader.GetDouble(5),
                    IsFullTextHit: reader.GetBoolean(6),
                    Score: Convert.ToDouble(reader.GetValue(7), CultureInfo.InvariantCulture)));
            }

            return hits;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    /// <summary>
    /// <c>đóng or sprint or task …</c> for <c>websearch_to_tsquery</c>. Only letters and digits survive, so nothing the
    /// user typed can inject websearch operators (quotes, leading <c>-</c>).
    /// </summary>
    /// <param name="text">The question.</param>
    /// <returns>OR-ed words, or the original text when it has no usable word.</returns>
    internal static string AnyWordQuery(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text + " ")
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }

            if (current.Length > 1) words.Add(current.ToString());
            current.Clear();
        }

        var distinct = words.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxQueryWords).ToList();
        return distinct.Count == 0 ? text : string.Join(" or ", distinct);
    }

    /// <summary>Caps the OR query so a pasted wall of text cannot produce a huge tsquery.</summary>
    private const int MaxQueryWords = 24;

    /// <summary>pgvector text form: <c>[0.1,0.2,…]</c>, culture-invariant.</summary>
    private static string ToVectorLiteral(float[] vector)
    {
        var builder = new StringBuilder(vector.Length * 10).Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(vector[i].ToString("R", CultureInfo.InvariantCulture));
        }

        return builder.Append(']').ToString();
    }
}
