using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

// Reporting-only storage is accessed through parameterized SQL, not an EF entity.
[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261121090000_AddFinancialReportingHistory")]
public sealed class AddFinancialReportingHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE survey.financial_report_history (
                sequence bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                source text NOT NULL,
                source_id uuid NOT NULL,
                order_id uuid NOT NULL,
                recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
                data jsonb NOT NULL,
                is_deleted boolean NOT NULL DEFAULT false
            );
            CREATE INDEX ix_financial_report_history_cutoff
                ON survey.financial_report_history(source, source_id, recorded_at DESC, sequence DESC);
            CREATE INDEX ix_financial_report_history_order ON survey.financial_report_history(order_id, sequence);
            CREATE UNIQUE INDEX uq_financial_report_original_sale ON survey.financial_report_history(order_id)
                WHERE source = 'original_service_publication';
            CREATE TABLE survey.financial_report_coverage (
                id integer PRIMARY KEY CHECK (id = 1), started_at timestamptz NOT NULL
            );
            INSERT INTO survey.financial_report_coverage VALUES (1, clock_timestamp());

            CREATE FUNCTION survey.financial_report_capture() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE row_data jsonb; safe_data jsonb; row_id uuid; order_key uuid;
            BEGIN
                IF TG_OP = 'DELETE' THEN row_data := to_jsonb(OLD); ELSE row_data := to_jsonb(NEW); END IF;
                order_key := COALESCE(row_data->>'survey_order_id', row_data->>'id')::uuid;
                row_id := COALESCE(row_data->>'id', row_data->>'survey_order_id')::uuid;
                -- Explicit allow-list: no account numbers, bank proof, AI payloads or personal data.
                SELECT jsonb_object_agg(key, value) INTO safe_data FROM jsonb_each(row_data)
                WHERE key = ANY(ARRAY['id','survey_order_id','order_number','scope','status',
                    'final_price','original_final_price','pricing_confirmed_at','created_at','updated_at',
                    'published_at','confirmed_at','amount','approved_amount','transferred_at','completed_at',
                    'received_at','occurred_at','recorded_at','origin_kind','origin_decision_id',
                    'provider','provider_transaction_id','provider_reference','purpose','service_code',
                    'booked_service_type','currency','deposit_obligation_id']);
                IF TG_OP = 'UPDATE' AND EXISTS (
                    SELECT 1 FROM survey.financial_report_history h
                    WHERE h.source = TG_TABLE_NAME AND h.source_id = row_id
                    AND h.sequence = (SELECT max(sequence) FROM survey.financial_report_history
                        WHERE source = TG_TABLE_NAME AND source_id = row_id)
                    AND h.data = safe_data AND NOT h.is_deleted
                ) THEN RETURN NEW; END IF;
                INSERT INTO survey.financial_report_history(source,source_id,order_id,data,is_deleted)
                    VALUES (TG_TABLE_NAME,row_id,order_key,safe_data,TG_OP = 'DELETE');
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END $body$;
            """);
        foreach (var table in new[] { "survey_orders", "order_commercial_snapshots", "survey_results",
                     "survey_payments", "survey_funding_receipts", "survey_funding_reversals",
                     "survey_refund_obligations", "quality_refund_cases", "cancellation_retentions" })
        {
            // Capture a known opening state at deployment, never backdate it as historical evidence.
            migrationBuilder.Sql($"""
                CREATE TRIGGER financial_report_capture AFTER INSERT OR UPDATE OR DELETE ON survey.{table}
                    FOR EACH ROW EXECUTE FUNCTION survey.financial_report_capture();
                INSERT INTO survey.financial_report_history(source,source_id,order_id,data)
                SELECT '{table}', COALESCE(j->>'id',j->>'survey_order_id')::uuid,
                    COALESCE(j->>'survey_order_id',j->>'id')::uuid,
                    (SELECT jsonb_object_agg(key,value) FROM jsonb_each(j)
                     WHERE key = ANY(ARRAY['id','survey_order_id','order_number','scope','status',
                       'final_price','original_final_price','pricing_confirmed_at','created_at','updated_at',
                       'published_at','confirmed_at','amount','approved_amount','transferred_at','completed_at',
                       'received_at','occurred_at','recorded_at','origin_kind','origin_decision_id',
                       'provider','provider_transaction_id','provider_reference','purpose','service_code',
                       'booked_service_type','currency','deposit_obligation_id']))
                FROM (SELECT to_jsonb(t) j FROM survey.{table} t) opening;
                """);
        }
        migrationBuilder.Sql("""
            CREATE FUNCTION survey.financial_report_publication() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE payload jsonb; order_key uuid; invoice jsonb;
            BEGIN
                payload := to_jsonb(NEW);
                IF payload->>'published_at' IS NULL THEN RETURN NEW; END IF;
                IF TG_OP = 'UPDATE' AND to_jsonb(OLD)->>'published_at' IS NOT NULL THEN RETURN NEW; END IF;
                order_key := COALESCE(payload->>'survey_order_id',payload->>'source_survey_order_id')::uuid;
                -- One order-level lock serializes MAP/COMBO publications and freezes the original invoice.
                SELECT to_jsonb(o) INTO invoice FROM survey.survey_orders o WHERE id = order_key FOR UPDATE;
                IF invoice->>'final_price' IS NOT NULL THEN
                    INSERT INTO survey.financial_report_history(source,source_id,order_id,data)
                    VALUES ('original_service_publication',order_key,order_key,
                        jsonb_build_object('published_at',payload->>'published_at','amount',invoice->'final_price'))
                    ON CONFLICT (order_id) WHERE source = 'original_service_publication' DO NOTHING;
                END IF;
                RETURN NEW;
            END $body$;
            CREATE TRIGGER financial_report_publication AFTER INSERT OR UPDATE ON survey.survey_results
                FOR EACH ROW EXECUTE FUNCTION survey.financial_report_publication();
            CREATE TRIGGER financial_report_publication AFTER INSERT OR UPDATE ON farm.farm_base_map_versions
                FOR EACH ROW EXECUTE FUNCTION survey.financial_report_publication();

            CREATE FUNCTION survey.financial_report_immutable() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Financial reporting history is append-only'; END $body$;
            CREATE TRIGGER financial_report_immutable BEFORE UPDATE OR DELETE ON survey.financial_report_history
                FOR EACH ROW EXECUTE FUNCTION survey.financial_report_immutable();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "survey_orders", "order_commercial_snapshots", "survey_results",
                     "survey_payments", "survey_funding_receipts", "survey_funding_reversals",
                     "survey_refund_obligations", "quality_refund_cases", "cancellation_retentions" })
            migrationBuilder.Sql($"DROP TRIGGER financial_report_capture ON survey.{table};");
        migrationBuilder.Sql("""
            DROP TRIGGER financial_report_publication ON survey.survey_results;
            DROP TRIGGER financial_report_publication ON farm.farm_base_map_versions;
            DROP FUNCTION survey.financial_report_publication();
            DROP TABLE survey.financial_report_history;
            DROP TABLE survey.financial_report_coverage;
            DROP FUNCTION survey.financial_report_capture();
            DROP FUNCTION survey.financial_report_immutable();
            """);
    }
}
