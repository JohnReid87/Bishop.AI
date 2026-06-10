using Bishop.App.Findings;
using FluentAssertions;

namespace Bishop.Tests.App.Findings;

public sealed class FindingsValidatorTests
{
    // ── Parse: empty / whitespace input ───────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Parse_EmptyOrWhitespace_ThrowsValidationException(string json)
    {
        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*empty*");
    }

    // ── Parse: malformed JSON ─────────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedJson_ThrowsValidationException()
    {
        var act = () => FindingsValidator.Parse("{bad json}");

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*malformed*");
    }

    // ── Parse: root element validation ────────────────────────────────────────

    [Theory]
    [InlineData("[\"a\"]")]
    [InlineData("\"string\"")]
    [InlineData("42")]
    public void Parse_RootNotObject_ThrowsValidationException(string json)
    {
        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*root must be an object*");
    }

    // ── Parse: 'findings' property ────────────────────────────────────────────

    [Fact]
    public void Parse_MissingFindingsProperty_ThrowsValidationException()
    {
        var act = () => FindingsValidator.Parse("""{"other": 1}""");

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*'findings' array*");
    }

    [Fact]
    public void Parse_FindingsNotArray_ThrowsValidationException()
    {
        var act = () => FindingsValidator.Parse("""{"findings": "nope"}""");

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*must be an array*");
    }

    // ── Parse: 'projectName' handling ─────────────────────────────────────────

    [Fact]
    public void Parse_ProjectNameAbsent_IsNullInDocument()
    {
        var result = FindingsValidator.Parse("""{"findings": []}""");

        result.ProjectName.Should().BeNull();
    }

    [Fact]
    public void Parse_ProjectNameNull_IsNullInDocument()
    {
        var result = FindingsValidator.Parse("""{"findings": [], "projectName": null}""");

        result.ProjectName.Should().BeNull();
    }

    [Theory]
    [InlineData("\"\"")]
    public void Parse_ProjectNameEmptyString_IsNullInDocument(string nameJson)
    {
        var result = FindingsValidator.Parse($$"""{"findings": [], "projectName": {{nameJson}}}""");

        result.ProjectName.Should().BeNull();
    }

    [Fact]
    public void Parse_ProjectNameNotString_ThrowsValidationException()
    {
        var act = () => FindingsValidator.Parse("""{"findings": [], "projectName": 42}""");

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*'projectName' must be a string*");
    }

    [Fact]
    public void Parse_ProjectNamePresent_ParsedCorrectly()
    {
        var result = FindingsValidator.Parse("""{"findings": [], "projectName": "MyProject"}""");

        result.ProjectName.Should().Be("MyProject");
    }

    // ── Parse: empty findings array ───────────────────────────────────────────

    [Fact]
    public void Parse_EmptyFindingsArray_ReturnsDocumentWithNoFindings()
    {
        var result = FindingsValidator.Parse("""{"findings": []}""");

        result.Findings.Should().BeEmpty();
    }

    // ── ParseFinding: element must be an object ────────────────────────────────

    [Fact]
    public void Parse_FindingElementNotObject_ThrowsValidationException()
    {
        var act = () => FindingsValidator.Parse("""{"findings": ["not-an-object"]}""");

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*findings[0] must be an object*");
    }

    // ── ParseFinding: required fields ─────────────────────────────────────────

    [Theory]
    [InlineData("title")]
    [InlineData("body")]
    [InlineData("outcome")]
    public void Parse_FindingMissingRequiredField_ThrowsValidationException(string field)
    {
        var fields = new Dictionary<string, string>
        {
            ["title"] = "\"The Title\"",
            ["body"] = "\"The body\"",
            ["outcome"] = "\"dismissed\""
        };
        fields.Remove(field);
        var json = $$"""{"findings": [{{{string.Join(", ", fields.Select(kv => $"\"{kv.Key}\": {kv.Value}"))}}}]}""";

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage($"*findings[0].{field} is required*");
    }

    [Theory]
    [InlineData("title")]
    [InlineData("body")]
    [InlineData("outcome")]
    public void Parse_FindingRequiredFieldNotString_ThrowsValidationException(string field)
    {
        var fields = new Dictionary<string, string>
        {
            ["title"] = "\"The Title\"",
            ["body"] = "\"The body\"",
            ["outcome"] = "\"dismissed\""
        };
        fields[field] = "123";
        var json = $$"""{"findings": [{{{string.Join(", ", fields.Select(kv => $"\"{kv.Key}\": {kv.Value}"))}}}]}""";

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage($"*findings[0].{field} must be a string*");
    }

    [Theory]
    [InlineData("title")]
    [InlineData("body")]
    [InlineData("outcome")]
    public void Parse_FindingRequiredFieldEmpty_ThrowsValidationException(string field)
    {
        var fields = new Dictionary<string, string>
        {
            ["title"] = "\"The Title\"",
            ["body"] = "\"The body\"",
            ["outcome"] = "\"dismissed\""
        };
        fields[field] = "\"\"";
        var json = $$"""{"findings": [{{{string.Join(", ", fields.Select(kv => $"\"{kv.Key}\": {kv.Value}"))}}}]}""";

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage($"*findings[0].{field} must not be empty*");
    }

    // ── ParseFinding: outcome validation ─────────────────────────────────────

    [Theory]
    [InlineData("dismissed")]
    [InlineData("parked")]
    [InlineData("carded:#1")]
    [InlineData("carded:#999")]
    public void Parse_ValidOutcome_ParsedCorrectly(string outcome)
    {
        var json = $$"""{"findings": [{"title": "T", "body": "B", "outcome": "{{outcome}}"}]}""";

        var result = FindingsValidator.Parse(json);

        result.Findings[0].Outcome.Should().Be(outcome);
    }

    [Theory]
    [InlineData("DISMISSED")]
    [InlineData("Parked")]
    [InlineData("carded:42")]
    [InlineData("carded:#")]
    [InlineData("unknown")]
    public void Parse_InvalidOutcome_ThrowsValidationException(string outcome)
    {
        var json = $$$"""{"findings": [{"title": "T", "body": "B", "outcome": "{{{outcome}}}"}]}""";

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*outcome must be 'dismissed'*");
    }

    // ── ParseFinding: optional string fields ─────────────────────────────────

    [Theory]
    [InlineData("severity")]
    [InlineData("location")]
    [InlineData("file")]
    [InlineData("rule")]
    [InlineData("symbol")]
    public void Parse_OptionalFieldAbsent_IsNullInFinding(string field)
    {
        var json = """{"findings": [{"title": "T", "body": "B", "outcome": "dismissed"}]}""";

        var result = FindingsValidator.Parse(json);
        var finding = result.Findings[0];

        GetOptionalField(finding, field).Should().BeNull();
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("location")]
    [InlineData("file")]
    [InlineData("rule")]
    [InlineData("symbol")]
    public void Parse_OptionalFieldNull_IsNullInFinding(string field)
    {
        var json = $$"""{"findings": [{"title": "T", "body": "B", "outcome": "dismissed", "{{field}}": null}]}""";

        var result = FindingsValidator.Parse(json);

        GetOptionalField(result.Findings[0], field).Should().BeNull();
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("location")]
    [InlineData("file")]
    [InlineData("rule")]
    [InlineData("symbol")]
    public void Parse_OptionalFieldEmpty_IsNullInFinding(string field)
    {
        var json = $$"""{"findings": [{"title": "T", "body": "B", "outcome": "dismissed", "{{field}}": ""}]}""";

        var result = FindingsValidator.Parse(json);

        GetOptionalField(result.Findings[0], field).Should().BeNull();
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("location")]
    [InlineData("file")]
    [InlineData("rule")]
    [InlineData("symbol")]
    public void Parse_OptionalFieldNotString_ThrowsValidationException(string field)
    {
        var json = $$"""{"findings": [{"title": "T", "body": "B", "outcome": "dismissed", "{{field}}": 42}]}""";

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage($"*findings[0].{field} must be a string when present*");
    }

    [Fact]
    public void Parse_AllOptionalFieldsPresent_ParsedCorrectly()
    {
        var json = """
            {
              "findings": [{
                "title": "T",
                "body": "B",
                "outcome": "carded:#7",
                "severity": "high",
                "location": "SomeMethod",
                "file": "Foo.cs",
                "rule": "SA001",
                "symbol": "MyClass"
              }]
            }
            """;

        var result = FindingsValidator.Parse(json);

        var f = result.Findings[0];
        f.Title.Should().Be("T");
        f.Body.Should().Be("B");
        f.Outcome.Should().Be("carded:#7");
        f.Severity.Should().Be("high");
        f.Location.Should().Be("SomeMethod");
        f.File.Should().Be("Foo.cs");
        f.Rule.Should().Be("SA001");
        f.Symbol.Should().Be("MyClass");
    }

    // ── Parse: multiple findings ───────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleFindings_AllParsedInOrder()
    {
        var json = """
            {
              "findings": [
                {"title": "First",  "body": "B1", "outcome": "dismissed"},
                {"title": "Second", "body": "B2", "outcome": "parked"},
                {"title": "Third",  "body": "B3", "outcome": "carded:#3"}
              ]
            }
            """;

        var result = FindingsValidator.Parse(json);

        result.Findings.Should().HaveCount(3);
        result.Findings[0].Title.Should().Be("First");
        result.Findings[1].Title.Should().Be("Second");
        result.Findings[2].Title.Should().Be("Third");
    }

    [Fact]
    public void Parse_SecondFindingInvalid_MessageIncludesCorrectIndex()
    {
        var json = """
            {
              "findings": [
                {"title": "Ok", "body": "B", "outcome": "dismissed"},
                "not-an-object"
              ]
            }
            """;

        var act = () => FindingsValidator.Parse(json);

        act.Should().Throw<FindingsValidationException>()
            .WithMessage("*findings[1]*");
    }

    // ── FindingsValidationException ────────────────────────────────────────────

    [Fact]
    public void FindingsValidationException_MessageConstructor_SetsMessage()
    {
        var ex = new FindingsValidationException("some error");

        ex.Message.Should().Be("some error");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void FindingsValidationException_InnerExceptionConstructor_SetsMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new FindingsValidationException("outer", inner);

        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetOptionalField(Finding f, string field) => field switch
    {
        "severity" => f.Severity,
        "location" => f.Location,
        "file" => f.File,
        "rule" => f.Rule,
        "symbol" => f.Symbol,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };
}
