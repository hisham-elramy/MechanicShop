using MechanicShop.Application.Features.Scheduling.Queries.GetDailySchedule;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Scheduling.Queries.GetDailySchedule;

public class GetDailyScheduleQueryTests
{
    [Fact]
    public void CacheKey_ShouldDiffer_WhenTimeZonesDiffer()
    {
        var date = new DateOnly(2030, 1, 1);
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

        Assert.NotEqual
        (
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, date).CacheKey,
            new GetDailyScheduleQuery(cairo, date).CacheKey
        );
    }

    [Fact]
    public void CacheKey_ShouldDiffer_WhenLaborDiffers()
    {
        var date = new DateOnly(2030, 1, 1);

        Assert.NotEqual
        (
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, date, Guid.NewGuid()).CacheKey,
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, date, Guid.NewGuid()).CacheKey
        );
    }

    [Fact]
    public void CacheKey_ShouldDiffer_WhenDatesDiffer()
    {
        Assert.NotEqual
        (
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, new DateOnly(2030, 1, 1)).CacheKey,
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, new DateOnly(2030, 1, 2)).CacheKey
        );
    }

    [Fact]
    public void Tags_ShouldContainWorkOrderTag()
    {
        Assert.Contains
        (
            "work-order",
            new GetDailyScheduleQuery(TimeZoneInfo.Utc, new DateOnly(2030, 1, 1)).Tags
        );
    }

}