using Bishop.Life.SchemaCodegen;
using FluentAssertions;

namespace Bishop.Life.Tests;

/// <summary>
/// Unit coverage for the C#→TypeScript schema emitter (card #1077). Driven
/// by inline source strings so the tests don't depend on the real schema
/// files — keeps assertions stable when the wire contract evolves.
/// </summary>
public class TypeScriptEmitterTests
{
    [Fact]
    public void Emit_PublicClass_WithJsonPropertyName_UsesAttributeName()
    {
        const string src = """
            using System.Text.Json.Serialization;
            namespace X;
            public sealed class Foo
            {
                [JsonPropertyName("bar")]
                public string Baz { get; set; } = "";
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("export interface Foo {");
        ts.Should().Contain("bar: string;");
        ts.Should().NotContain("baz");
    }

    [Fact]
    public void Emit_PositionalRecord_GeneratesInterfaceWithCamelCaseFields()
    {
        const string src = """
            namespace X;
            public sealed record SpeakEnvelope(string Type, int DurationMs);
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("export interface SpeakEnvelope {");
        ts.Should().Contain("type: string;");
        ts.Should().Contain("durationMs: number;");
    }

    [Fact]
    public void Emit_NullableReference_AddsOptionalAndNullUnion()
    {
        const string src = """
            namespace X;
            public sealed class Bar
            {
                public string? Note { get; set; }
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("note?: string | null;");
    }

    [Fact]
    public void Emit_NullableValueType_AddsOptionalAndNullUnion()
    {
        const string src = """
            using System;
            namespace X;
            public sealed class Bar
            {
                public DateTimeOffset? At { get; set; }
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("at?: string | null;");
    }

    [Fact]
    public void Emit_ListOfT_MapsToArray()
    {
        const string src = """
            using System.Collections.Generic;
            namespace X;
            public sealed class Bag
            {
                public List<string> Items { get; set; } = new();
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("items: string[];");
    }

    [Fact]
    public void Emit_Enum_EmitsCamelCaseStringLiteralUnion()
    {
        const string src = """
            namespace X;
            public enum Horizon { Today, ThisWeek, ThisMonth, Someday }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("export type Horizon =");
        ts.Should().Contain("| \"today\"");
        ts.Should().Contain("| \"thisWeek\"");
        ts.Should().Contain("| \"thisMonth\"");
        ts.Should().Contain("| \"someday\";");
    }

    [Fact]
    public void Emit_NumericPrimitives_MapToNumber()
    {
        const string src = """
            namespace X;
            public sealed class Nums
            {
                public int A { get; set; }
                public long B { get; set; }
                public double C { get; set; }
                public decimal D { get; set; }
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("a: number;");
        ts.Should().Contain("b: number;");
        ts.Should().Contain("c: number;");
        ts.Should().Contain("d: number;");
    }

    [Fact]
    public void Emit_UserDefinedTypeReference_PassesThroughByName()
    {
        const string src = """
            using System.Collections.Generic;
            namespace X;
            public sealed class Area { public string Id { get; set; } = ""; }
            public sealed class LifePlan { public List<Area> Areas { get; set; } = new(); }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("areas: Area[];");
    }

    [Fact]
    public void Emit_StaticClass_IsSkipped()
    {
        // GoalHorizon-style helper: static class with consts. Has no instance
        // state, so emitting it would just produce an empty TS interface.
        const string src = """
            namespace X;
            public static class Helpers
            {
                public const string Month = "month";
            }
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().NotContain("Helpers");
    }

    [Fact]
    public void Emit_InternalTypes_AreNotEmitted()
    {
        const string src = """
            namespace X;
            internal sealed record Hidden(string Type);
            public sealed record Visible(string Type);
            """;

        var ts = new TypeScriptEmitter().Emit(new[] { src });

        ts.Should().Contain("export interface Visible {");
        ts.Should().NotContain("Hidden");
    }

    [Fact]
    public void Emit_TwoSources_InterfacesSortedAlphabetically()
    {
        const string a = "namespace X; public sealed record Zeta(string Name);";
        const string b = "namespace X; public sealed record Alpha(string Name);";

        var ts = new TypeScriptEmitter().Emit(new[] { a, b });

        var alphaIdx = ts.IndexOf("interface Alpha");
        var zetaIdx = ts.IndexOf("interface Zeta");
        alphaIdx.Should().BeGreaterThan(0);
        zetaIdx.Should().BeGreaterThan(alphaIdx);
    }

    [Fact]
    public void Emit_GeneratedHeader_IsPresent()
    {
        var ts = new TypeScriptEmitter().Emit(new[] { "namespace X; public sealed record Foo(string Bar);" });

        ts.Should().StartWith("// <auto-generated>");
        ts.Should().Contain("Bishop.Life.SchemaCodegen");
    }
}
