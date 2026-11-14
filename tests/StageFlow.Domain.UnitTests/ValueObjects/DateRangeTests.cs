using FluentAssertions;
using StageFlow.Domain.ValueObjects;
using Xunit;

namespace StageFlow.Domain.UnitTests.ValueObjects;

public class DateRangeTests
{
    [Fact]
    public void OverlapsWith_WhenOverlapping_ShouldReturnTrue()
    {
        var start = new DateTime(2026, 6, 1, 10, 0, 0);
        var end = new DateTime(2026, 6, 1, 11, 0, 0);
        var range1 = new DateRange(start, end);

        var range2 = new DateRange(start.AddMinutes(30), end.AddMinutes(30));

        range1.OverlapsWith(range2).Should().BeTrue();
        range2.OverlapsWith(range1).Should().BeTrue();
    }

    [Fact]
    public void OverlapsWith_WhenConsecutive_ShouldReturnFalse()
    {
        var t1 = new DateTime(2026, 6, 1, 10, 0, 0);
        var t2 = new DateTime(2026, 6, 1, 11, 0, 0);
        var t3 = new DateTime(2026, 6, 1, 12, 0, 0);

        var range1 = new DateRange(t1, t2);
        var range2 = new DateRange(t2, t3);

        range1.OverlapsWith(range2).Should().BeFalse();
        range2.OverlapsWith(range1).Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenEndBeforeStart_ShouldReturnError()
    {
        var start = new DateTime(2026, 6, 1);
        var end = new DateTime(2026, 5, 1);

        var (isValid, error) = DateRange.Validate(start, end);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}

