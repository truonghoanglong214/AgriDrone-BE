using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

/// <summary>
/// Step7D yêu cầu đơn COMPLETED cùng lúc phát hành kết quả Plant Health. Đơn "thanh toán sau bay"
/// (RegistrationV26PostFlightSettlement) lại chủ động chuyển sang AWAITING_PRICING khi phát hành và chỉ COMPLETED sau khi
/// trả phần còn lại ⇒ mọi lần phát hành của đơn này bị trigger chặn lúc COMMIT (lỗi 500). Cho phép thêm hai trạng thái
/// chờ thanh toán của đơn post-flight; đơn thường vẫn phải COMPLETED như cũ.
/// </summary>
[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261118090000_AllowPostFlightPlantHealthPublication")]
public sealed class AllowPostFlightPlantHealthPublication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Function("""
            (o.status = 'COMPLETED' OR (o.post_flight_settlement AND o.status IN ('AWAITING_PRICING', 'AWAITING_PAYMENT')))
            """));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Function("o.status = 'COMPLETED'"));

    // Giữ nguyên thân hàm của Step7D, chỉ thay điều kiện trạng thái đơn.
    private static string Function(string orderStatusCondition) => $$"""
        CREATE OR REPLACE FUNCTION survey.check_health_publication_commit() RETURNS trigger
        LANGUAGE plpgsql AS $guard$
        DECLARE result_id uuid;
        BEGIN
            IF TG_TABLE_NAME = 'survey_results' THEN
                IF NEW.result_nature IS DISTINCT FROM 'PRELIMINARY_SUSPECTED' OR NEW.status <> 'PUBLISHED' THEN RETURN NEW; END IF;
                result_id := NEW.id;
            ELSE
                result_id := NEW.survey_result_id;
            END IF;
            IF NOT EXISTS (
                SELECT 1 FROM survey.plant_health_publications p
                JOIN survey.survey_results r ON r.id = p.survey_result_id AND r.tenant_id = p.tenant_id AND r.farm_id = p.farm_id
                JOIN survey.survey_orders o ON o.id = r.survey_order_id AND o.tenant_id = r.tenant_id AND o.farm_id = r.farm_id
                WHERE r.id = result_id AND r.status = 'PUBLISHED' AND {{orderStatusCondition.Trim()}}
                  AND r.result_nature = 'PRELIMINARY_SUSPECTED' AND p.batch_id = r.approved_batch_id
                  AND p.approval_fingerprint = r.approval_fingerprint
                  AND p.published_by = r.published_by AND p.published_at = r.published_at
            ) THEN
                RAISE EXCEPTION 'Publication, frozen approval and completed Order must commit together.' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END $guard$;
        """;
}
