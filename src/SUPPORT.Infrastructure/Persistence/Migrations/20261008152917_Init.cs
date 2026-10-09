using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace SUPPORT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            // Hand-written: kb_chunk.search_tsv is a generated column and PostgreSQL only accepts IMMUTABLE
            // functions there. unaccent() is merely STABLE, so wrap it with the dictionary pinned explicitly.
            // Must exist before kb_chunk is created below.
            migrationBuilder.Sql($"""
                CREATE OR REPLACE FUNCTION {SupportDbContext.ImmutableUnaccentFunction}(text)
                RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;
                """);

            migrationBuilder.CreateTable(
                name: "chat_conversation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_conversation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kb_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<short>(type: "smallint", nullable: false),
                    source_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    route_hint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggestions = table.Column<List<string>>(type: "text[]", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kb_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kb_ingestion_run",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    docs_scanned = table.Column<int>(type: "integer", nullable: false),
                    docs_changed = table.Column<int>(type: "integer", nullable: false),
                    docs_archived = table.Column<int>(type: "integer", nullable: false),
                    chunks_written = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kb_ingestion_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    citations = table.Column<string>(type: "jsonb", nullable: false),
                    page_route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                    completion_tokens = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    top_score = table.Column<float>(type: "real", nullable: true),
                    is_interrupted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_message_chat_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "chat_conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_chunk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    heading_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    search_tsv = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple', support_unaccent(heading_path || ' ' || content))", stored: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kb_chunk", x => x.id);
                    table.ForeignKey(
                        name: "fk_kb_chunk_kb_document_document_id",
                        column: x => x.document_id,
                        principalTable: "kb_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_feedback",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_feedback", x => x.message_id);
                    table.CheckConstraint("ck_chat_feedback_rating", "rating IN (-1, 1)");
                    table.ForeignKey(
                        name: "fk_chat_feedback_chat_message_message_id",
                        column: x => x.message_id,
                        principalTable: "chat_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_conversation_product_org_id_user_id_last_message_at",
                table: "chat_conversation",
                columns: new[] { "product", "org_id", "user_id", "last_message_at" },
                descending: new[] { false, false, false, true },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_conversation_id_created_at",
                table: "chat_message",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_kb_chunk_document_id_chunk_index",
                table: "kb_chunk",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kb_chunk_embedding",
                table: "kb_chunk",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "ix_kb_chunk_product_org_id_module",
                table: "kb_chunk",
                columns: new[] { "product", "org_id", "module" });

            migrationBuilder.CreateIndex(
                name: "ix_kb_chunk_search_tsv",
                table: "kb_chunk",
                column: "search_tsv")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_kb_document_product_org_id_source_key",
                table: "kb_document",
                columns: new[] { "product", "org_id", "source_key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_kb_ingestion_run_product_started_at",
                table: "kb_ingestion_run",
                columns: new[] { "product", "started_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_feedback");

            migrationBuilder.DropTable(
                name: "kb_chunk");

            migrationBuilder.DropTable(
                name: "kb_ingestion_run");

            migrationBuilder.DropTable(
                name: "chat_message");

            migrationBuilder.DropTable(
                name: "kb_document");

            migrationBuilder.DropTable(
                name: "chat_conversation");

            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS {SupportDbContext.ImmutableUnaccentFunction}(text);");
        }
    }
}
