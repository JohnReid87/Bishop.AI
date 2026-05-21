using Bishop.App.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class StreamJsonFormatterTests
{
    [Fact]
    public void Format_Returns_Null_For_Blank_Line()
    {
        new StreamJsonFormatter().Format("").Should().BeNull();
        new StreamJsonFormatter().Format("   ").Should().BeNull();
    }

    [Fact]
    public void Format_Returns_Null_For_Invalid_Json()
    {
        new StreamJsonFormatter().Format("not json").Should().BeNull();
    }

    [Fact]
    public void Format_Returns_Null_For_Unknown_Event_Type()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"system","subtype":"init"}""").Should().BeNull();
        sut.Format("""{"type":"ping"}""").Should().BeNull();
    }

    [Fact]
    public void Format_Returns_Null_When_Json_Is_Not_Object()
    {
        new StreamJsonFormatter().Format("[1,2,3]").Should().BeNull();
    }

    [Fact]
    public void Format_Returns_Null_When_Type_Field_Missing()
    {
        new StreamJsonFormatter().Format("""{"foo":"bar"}""").Should().BeNull();
    }

    [Fact]
    public void Format_Assistant_Text_Prefixes_With_Ellipsis_Marker()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"Hello there"}]}}""";

        sut.Format(line).Should().Be("… Hello there");
    }

    [Fact]
    public void Format_Assistant_Text_Truncates_To_120_Chars_With_Ellipsis()
    {
        var sut = new StreamJsonFormatter();
        var longText = new string('a', 200);
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\""
            + longText + "\"}]}}";

        var output = sut.Format(line);

        output.Should().NotBeNull();
        output!.Should().StartWith("… ");
        output.Should().EndWith("…");
        output.Length.Should().Be("… ".Length + 120 + 1);
    }

    [Fact]
    public void Format_Assistant_Text_Collapses_Newlines_And_Tabs_Into_Single_Spaces()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"line1\nline2\t\tend"}]}}""";

        sut.Format(line).Should().Be("… line1 line2 end");
    }

    [Theory]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}}""")]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}}""")]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Grep","input":{"pattern":"TODO"}}]}}""")]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"TodoWrite","input":{"todos":[]}}]}}""")]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":"not an object"}]}}""")]
    public void Format_Tool_Use_Block_Produces_No_Output(string line)
    {
        new StreamJsonFormatter().Format(line).Should().BeNull();
    }

    [Fact]
    public void Format_Multiple_Content_Blocks_With_Tool_Use_Returns_Only_Text_Lines()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"hi"},{"type":"tool_use","name":"Bash","input":{"command":"ls"}},{"type":"text","text":"bye"}]}}""";

        sut.Format(line).Should().Be("… hi" + Environment.NewLine + "… bye");
    }

    [Fact]
    public void Format_Tool_Result_Without_Error_Flag_Is_Dropped()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","content":"ok"}]}}""";

        sut.Format(line).Should().BeNull();
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_String_Content_Is_Formatted()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":"command not found"}]}}""";

        sut.Format(line).Should().Be("[error] command not found");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_Array_Content_Reads_First_Text_Block()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[{"type":"text","text":"boom"}]}]}}""";

        sut.Format(line).Should().Be("[error] boom");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_But_No_Detail_Uses_Placeholder()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true}]}}""";

        sut.Format(line).Should().Be("[error] (no detail)");
    }

    [Fact]
    public void Format_Result_Includes_Duration_ToolUseCount_And_Cost()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"a"}}]}}""");
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"b"}}]}}""");

        var result = sut.Format("""{"type":"result","duration_ms":12500,"total_cost_usd":0.0345,"num_turns":3}""");

        result.Should().Be("done in 12.5s, 2 tool uses, $0.0345");
    }

    [Fact]
    public void Format_Result_Without_Cost_Omits_Cost_Segment()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"a"}}]}}""");

        var result = sut.Format("""{"type":"result","duration_ms":900}""");

        result.Should().Be("done in 900ms, 1 tool use");
    }

    [Fact]
    public void Format_Result_Without_Duration_Says_done()
    {
        var sut = new StreamJsonFormatter();

        var result = sut.Format("""{"type":"result","total_cost_usd":0.01}""");

        result.Should().Be("done, 0 tool uses, $0.01");
    }

    [Fact]
    public void Format_Result_Duration_Above_One_Minute_Uses_Mm_Ss()
    {
        var sut = new StreamJsonFormatter();

        var result = sut.Format("""{"type":"result","duration_ms":125000}""");

        result.Should().Be("done in 2m5s, 0 tool uses");
    }

    [Fact]
    public void Format_Counts_Tool_Uses_Across_Multiple_Blocks_And_Lines()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"a"}},{"type":"tool_use","name":"Read","input":{"file_path":"x.cs"}}]}}""");
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"x.cs"}}]}}""");

        var result = sut.Format("""{"type":"result","duration_ms":1000}""");

        result.Should().Be("done in 1.0s, 3 tool uses");
    }

    [Fact]
    public void Format_Result_Duration_At_Or_Above_One_Hour_Uses_Hh_Mm()
    {
        var sut = new StreamJsonFormatter();

        var result = sut.Format("""{"type":"result","duration_ms":5400000}""");

        result.Should().Be("done in 1h30m, 0 tool uses");
    }

    [Fact]
    public void Format_Assistant_Text_With_Leading_Whitespace_Strips_Leading_Space()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"  hello"}]}}""";

        sut.Format(line).Should().Be("… hello");
    }

    [Fact]
    public void Format_Assistant_Text_With_Trailing_Whitespace_Strips_Trailing_Space()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"hello  "}]}}""";

        sut.Format(line).Should().Be("… hello");
    }

    [Fact]
    public void Format_Assistant_Returns_Null_When_Message_Field_Is_Missing()
    {
        new StreamJsonFormatter().Format("""{"type":"assistant"}""").Should().BeNull();
    }

    [Fact]
    public void Format_Assistant_Returns_Null_When_Message_Is_Not_An_Object()
    {
        new StreamJsonFormatter().Format("""{"type":"assistant","message":"not an object"}""").Should().BeNull();
    }

    [Fact]
    public void Format_Assistant_Returns_Null_When_Content_Is_Not_An_Array()
    {
        new StreamJsonFormatter().Format("""{"type":"assistant","message":{"content":"not an array"}}""").Should().BeNull();
    }

    [Fact]
    public void Format_Assistant_Returns_Null_For_Unknown_Content_Block_Type()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"image","source":{}}]}}""";

        sut.Format(line).Should().BeNull();
    }

    [Fact]
    public void Totals_IsNull_BeforeResultEvent()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"hi"}]}}""");

        sut.Totals.Should().BeNull();
    }

    [Fact]
    public void Totals_PopulatesFromResultEvent_CostAndUsage()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","total_cost_usd":0.12,"usage":{"input_tokens":8100,"output_tokens":2400}}""");

        sut.Totals.Should().Be(new ClaudeRunTotals(0.12m, 8100, 2400));
    }

    [Fact]
    public void Totals_PopulatesFromResultEvent_CostOnly_WhenUsageMissing()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","total_cost_usd":0.05}""");

        sut.Totals.Should().Be(new ClaudeRunTotals(0.05m, 0, 0));
    }

    [Fact]
    public void Totals_StaysNull_WhenResultHasNoCostOrUsage()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","duration_ms":900}""");

        sut.Totals.Should().BeNull();
    }

    [Fact]
    public void Totals_StaysNull_WhenResultUsageIsNotAnObject()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","usage":"nope"}""");

        sut.Totals.Should().BeNull();
    }

    [Fact]
    public void Totals_IgnoresNonNumericUsageFields()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","total_cost_usd":0.10,"usage":{"input_tokens":"oops","output_tokens":50}}""");

        sut.Totals.Should().Be(new ClaudeRunTotals(0.10m, 0, 50));
    }
}
