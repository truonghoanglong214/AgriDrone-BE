using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

// The release read model is accessed by SQL, like financial_report_history.
public sealed partial class AddV3SurveyReleases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE survey.survey_refund_obligations
                DROP CONSTRAINT fk_survey_refund_obligations_decision_order,
                ADD COLUMN cancellation_decision_id uuid GENERATED ALWAYS AS (CASE WHEN origin_kind <> 'OVERPAYMENT'::system.survey_refund_origin_kind THEN origin_decision_id END) STORED,
                ADD COLUMN overpayment_invoice_revision_id uuid GENERATED ALWAYS AS (CASE WHEN origin_kind = 'OVERPAYMENT'::system.survey_refund_origin_kind THEN origin_decision_id END) STORED,
                ADD CONSTRAINT fk_survey_refund_obligations_decision_order
                    FOREIGN KEY (cancellation_decision_id, survey_order_id)
                    REFERENCES survey.survey_cancellation_decisions(id, survey_order_id) ON DELETE RESTRICT,
                ADD CONSTRAINT fk_survey_refund_obligations_overpayment_invoice
                    FOREIGN KEY (overpayment_invoice_revision_id, survey_order_id)
                    REFERENCES survey.survey_invoice_revisions(id, survey_order_id) ON DELETE RESTRICT;
            DROP INDEX survey."IX_survey_refund_obligations_origin_decision_id_survey_order_id";
            CREATE INDEX ix_survey_refund_obligations_cancellation_order
                ON survey.survey_refund_obligations(cancellation_decision_id, survey_order_id);
            CREATE INDEX ix_survey_refund_obligations_overpayment_invoice_order
                ON survey.survey_refund_obligations(overpayment_invoice_revision_id, survey_order_id);
            CREATE TABLE survey.v3_survey_releases (
                id uuid PRIMARY KEY,
                survey_order_id uuid NOT NULL UNIQUE,
                tenant_id uuid NOT NULL,
                farm_id uuid NOT NULL,
                approval_id uuid NOT NULL,
                invoice_id uuid NOT NULL,
                map_id uuid NOT NULL REFERENCES farm.farm_base_map_versions(id),
                notification_id uuid NOT NULL UNIQUE REFERENCES notification.notifications(id),
                published_at timestamptz NOT NULL,
                media_ids uuid[] NOT NULL,
                proof jsonb NOT NULL CHECK (jsonb_typeof(proof) = 'object'),
                reconciliation_snapshot jsonb NOT NULL,
                UNIQUE (id, survey_order_id, tenant_id, farm_id),
                FOREIGN KEY (survey_order_id, tenant_id, farm_id)
                    REFERENCES survey.survey_orders(id, tenant_id, farm_id),
                FOREIGN KEY (approval_id, survey_order_id, tenant_id, farm_id)
                    REFERENCES survey.survey_internal_approved_result_revisions(id, survey_order_id, tenant_id, farm_id),
                FOREIGN KEY (invoice_id, survey_order_id, tenant_id, farm_id)
                    REFERENCES survey.survey_invoice_revisions(id, survey_order_id, tenant_id, farm_id)
            );
            CREATE INDEX ix_v3_survey_releases_farm_history
                ON survey.v3_survey_releases(tenant_id, farm_id, published_at, id);
            ALTER TABLE survey.quality_refund_cases ADD CONSTRAINT fk_quality_refund_v3_release
                FOREIGN KEY (v3_publication_id, survey_order_id, tenant_id, farm_id)
                REFERENCES survey.v3_survey_releases(id, survey_order_id, tenant_id, farm_id);

            CREATE OR REPLACE FUNCTION survey.financial_report_publication() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE payload jsonb; order_key uuid; invoice jsonb;
            BEGIN
                payload := to_jsonb(NEW);
                IF payload->>'published_at' IS NULL THEN RETURN NEW; END IF;
                IF TG_OP = 'UPDATE' AND to_jsonb(OLD)->>'published_at' IS NOT NULL THEN RETURN NEW; END IF;
                order_key := COALESCE(payload->>'survey_order_id',payload->>'source_survey_order_id')::uuid;
                SELECT to_jsonb(o) INTO invoice FROM survey.survey_orders o WHERE id = order_key FOR UPDATE;
                IF invoice->>'final_price' IS NOT NULL AND invoice->>'scope' <> 'V3' THEN
                    INSERT INTO survey.financial_report_history(source,source_id,order_id,data)
                    VALUES ('original_service_publication',order_key,order_key,
                        jsonb_build_object('published_at',payload->>'published_at','amount',invoice->'final_price'))
                    ON CONFLICT (order_id) WHERE source = 'original_service_publication' DO NOTHING;
                END IF;
                RETURN NEW;
            END $body$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM survey.v3_survey_releases) THEN
                    RAISE EXCEPTION 'Cannot roll back while released V3 results exist.';
                END IF;
            END $$;
            ALTER TABLE survey.quality_refund_cases DROP CONSTRAINT fk_quality_refund_v3_release;
            DROP TABLE survey.v3_survey_releases;
            ALTER TABLE survey.survey_refund_obligations
                DROP CONSTRAINT fk_survey_refund_obligations_decision_order,
                DROP CONSTRAINT fk_survey_refund_obligations_overpayment_invoice,
                DROP COLUMN cancellation_decision_id,
                DROP COLUMN overpayment_invoice_revision_id,
                ADD CONSTRAINT fk_survey_refund_obligations_decision_order
                    FOREIGN KEY (origin_decision_id, survey_order_id)
                    REFERENCES survey.survey_cancellation_decisions(id, survey_order_id) ON DELETE RESTRICT;
            CREATE INDEX "IX_survey_refund_obligations_origin_decision_id_survey_order_id"
                ON survey.survey_refund_obligations(origin_decision_id, survey_order_id);
            CREATE OR REPLACE FUNCTION survey.financial_report_publication() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE payload jsonb; order_key uuid; invoice jsonb;
            BEGIN
                payload := to_jsonb(NEW);
                IF payload->>'published_at' IS NULL THEN RETURN NEW; END IF;
                IF TG_OP = 'UPDATE' AND to_jsonb(OLD)->>'published_at' IS NOT NULL THEN RETURN NEW; END IF;
                order_key := COALESCE(payload->>'survey_order_id',payload->>'source_survey_order_id')::uuid;
                SELECT to_jsonb(o) INTO invoice FROM survey.survey_orders o WHERE id = order_key FOR UPDATE;
                IF invoice->>'final_price' IS NOT NULL THEN
                    INSERT INTO survey.financial_report_history(source,source_id,order_id,data)
                    VALUES ('original_service_publication',order_key,order_key,
                        jsonb_build_object('published_at',payload->>'published_at','amount',invoice->'final_price'))
                    ON CONFLICT (order_id) WHERE source = 'original_service_publication' DO NOTHING;
                END IF;
                RETURN NEW;
            END $body$;
            """);
    }
}
