using AgriDrone.Modules.Farms.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.ToTable(
            "farms",
            "farm",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Top-level farm entity; stores location and optional farm polygon boundary.");
                tableBuilder.HasCheckConstraint(
                    "ck_farms_area_nonnegative",
                    "area_hectares IS NULL OR area_hectares >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_farms_boundary_valid",
                    "boundary IS NULL OR ST_IsValid(boundary)");
            });

        builder.HasKey(farm => farm.Id).HasName("pk_farms");

        builder.Property(farm => farm.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(farm => farm.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(farm => farm.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(farm => farm.Address)
            .HasColumnName("address")
            .HasColumnType("text");

        builder.Property(farm => farm.Boundary)
            .HasColumnName("boundary")
            .HasColumnType("geometry(Polygon,4326)");

        builder.Property(farm => farm.CenterPoint)
            .HasColumnName("center_point")
            .HasColumnType("geometry(Point,4326)");

        builder.Property(farm => farm.AreaHectares)
            .HasColumnName("area_hectares")
            .HasColumnType("numeric(12,4)")
            .HasPrecision(12, 4);

        builder.Property(farm => farm.Status)
            .HasColumnName("status")
            .HasColumnType("system.general_status")
            .HasDefaultValueSql("'ACTIVE'::system.general_status")
            .IsRequired();

        builder.Property(farm => farm.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(farm => farm.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(farm => farm.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(farm => farm.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(farm => farm.Code)
            .HasDatabaseName("ux_farms_code_active")
            .HasFilter("deleted_at IS NULL")
            .IsUnique();

        builder.HasIndex(farm => farm.Boundary)
            .HasDatabaseName("ix_farms_boundary_gist")
            .HasMethod("gist");

        builder.HasIndex(farm => farm.CenterPoint)
            .HasDatabaseName("ix_farms_center_point_gist")
            .HasMethod("gist");
    }
}
