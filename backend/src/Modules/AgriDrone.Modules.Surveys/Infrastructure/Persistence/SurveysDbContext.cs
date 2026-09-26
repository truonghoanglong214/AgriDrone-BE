using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence;

internal sealed class SurveysDbContext(DbContextOptions<SurveysDbContext> options)
    : DbContext(options)
{
    public DbSet<SurveyService> SurveyServices => Set<SurveyService>();
    public DbSet<SurveyServicePrice> SurveyServicePrices => Set<SurveyServicePrice>();
    public DbSet<SurveyRequest> SurveyRequests => Set<SurveyRequest>();
    public DbSet<SurveyRequestReview> SurveyRequestReviews => Set<SurveyRequestReview>();
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<SurveyAppointment> SurveyAppointments => Set<SurveyAppointment>();
    public DbSet<SurveyPayment> SurveyPayments => Set<SurveyPayment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PriceAdjustment> PriceAdjustments => Set<PriceAdjustment>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<HarvestReadinessAssessment> HarvestReadinessAssessments => Set<HarvestReadinessAssessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("survey");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveysDbContext).Assembly);
    }
}
