using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifePlanJsonTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var plan = SamplePlan();

        var json = LifePlanJson.Serialize(plan);
        var roundTripped = LifePlanJson.Deserialize(json);

        roundTripped.Should().BeEquivalentTo(plan);
    }

    [Fact]
    public void Serialize_IsIndented()
    {
        var plan = SamplePlan();

        var json = LifePlanJson.Serialize(plan);

        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }

    [Fact]
    public void Serialize_PreservesPropertyOrderMatchingSchema()
    {
        var plan = SamplePlan();

        var json = LifePlanJson.Serialize(plan);

        var schemaIdx = json.IndexOf("\"schema\"", StringComparison.Ordinal);
        var metaIdx = json.IndexOf("\"meta\"", StringComparison.Ordinal);
        var areasIdx = json.IndexOf("\"areas\"", StringComparison.Ordinal);
        var inboxIdx = json.IndexOf("\"inbox\"", StringComparison.Ordinal);
        var standupsIdx = json.IndexOf("\"standups\"", StringComparison.Ordinal);

        schemaIdx.Should().BeLessThan(metaIdx);
        metaIdx.Should().BeLessThan(areasIdx);
        areasIdx.Should().BeLessThan(inboxIdx);
        inboxIdx.Should().BeLessThan(standupsIdx);
    }

    [Fact]
    public void Serialize_ActionHorizon_IsCamelCaseString()
    {
        var plan = SamplePlan();
        plan.Areas[0].Goals[0].Actions[0].Horizon = Horizon.ThisWeek;

        var json = LifePlanJson.Serialize(plan);

        json.Should().Contain("\"horizon\": \"thisWeek\"");
    }

    [Fact]
    public void RoundTrip_PreservesAllActionHorizonValues()
    {
        foreach (var h in Enum.GetValues<Horizon>())
        {
            var plan = SamplePlan();
            plan.Areas[0].Goals[0].Actions[0].Horizon = h;

            var json = LifePlanJson.Serialize(plan);
            var roundTripped = LifePlanJson.Deserialize(json);

            roundTripped.Areas[0].Goals[0].Actions[0].Horizon.Should().Be(h);
        }
    }

    [Fact]
    public void Deserialize_ActionWithoutHorizon_DefaultsToThisWeek()
    {
        const string json = """
        {
          "schema": "bishop.life/v1",
          "meta": { "createdAt": "2026-06-08T08:00:00Z", "lastStandupAt": null },
          "areas": [
            {
              "id": "area-a", "name": "A", "color": "#abcdef",
              "goals": [
                {
                  "id": "g1", "name": "G1", "horizon": null,
                  "actions": [
                    {
                      "id": "act-1", "title": "Legacy action",
                      "starred": false, "done": false,
                      "createdAt": "2026-06-01T00:00:00Z", "completedAt": null
                    }
                  ]
                }
              ]
            }
          ],
          "inbox": [],
          "standups": []
        }
        """;

        var plan = LifePlanJson.Deserialize(json);

        plan.Areas[0].Goals[0].Actions[0].Horizon.Should().Be(Horizon.ThisWeek);
    }

    [Fact]
    public void Deserialize_NullHorizon_IsAccepted()
    {
        const string json = """
        {
          "schema": "bishop.life/v1",
          "meta": { "createdAt": "2026-06-08T08:00:00Z", "lastStandupAt": null },
          "areas": [
            {
              "id": "area-a", "name": "A", "color": "#abcdef",
              "goals": [
                { "id": "g1", "name": "G1", "horizon": null, "actions": [] }
              ]
            }
          ],
          "inbox": [],
          "standups": []
        }
        """;

        var plan = LifePlanJson.Deserialize(json);

        plan.Areas[0].Goals[0].Horizon.Should().BeNull();
        plan.Meta.LastStandupAt.Should().BeNull();
    }

    private static LifePlan SamplePlan() => new()
    {
        Schema = "bishop.life/v1",
        Meta = new Meta
        {
            CreatedAt = new DateTimeOffset(2026, 6, 8, 8, 0, 0, TimeSpan.Zero),
            LastStandupAt = new DateTimeOffset(2026, 6, 8, 8, 30, 0, TimeSpan.Zero),
        },
        Areas =
        {
            new Area
            {
                Id = "area-finances",
                Name = "Finances",
                Color = "#a8b3c4",
                Goals =
                {
                    new Goal
                    {
                        Id = "goal-emergency-fund",
                        Name = "Build 6-month emergency fund",
                        Horizon = "2026-12",
                        Actions =
                        {
                            new LifeAction
                            {
                                Id = "act-1",
                                Title = "Move £500 to savings",
                                Starred = true,
                                Done = false,
                                Horizon = Horizon.Today,
                                CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                                CompletedAt = null,
                            },
                        },
                    },
                },
            },
        },
        Inbox =
        {
            new InboxItem
            {
                Id = "ibx-1",
                Text = "Look into ISA limits",
                CapturedAt = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
            },
        },
        Standups =
        {
            new Standup
            {
                Id = "su-1",
                At = new DateTimeOffset(2026, 6, 8, 8, 30, 0, TimeSpan.Zero),
                Reflection = "Steady day.",
                FocusToday = { "act-1" },
            },
        },
    };
}
