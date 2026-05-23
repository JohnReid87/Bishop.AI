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
    public void Format_Result_Includes_Duration_And_ToolUseCount()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"a"}}]}}""");
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"b"}}]}}""");

        var result = sut.Format("""{"type":"result","duration_ms":12500,"total_cost_usd":0.0345,"num_turns":3}""");

        result.Should().Be("done in 12.5s, 2 tool uses");
    }

    [Fact]
    public void Format_Result_With_Duration_And_ToolUseCount()
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

        result.Should().Be("done, 0 tool uses");
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
    public void Totals_PopulatesFromAccumulatedAssistantUsage()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":5000,"output_tokens":1500},"content":[{"type":"text","text":"hi"}]}}""");
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":3100,"output_tokens":900},"content":[{"type":"text","text":"bye"}]}}""");
        sut.Format("""{"type":"result","total_cost_usd":0.12}""");

        sut.Totals.Should().Be(new ClaudeRunTotals(8100, 2400));
    }

    [Fact]
    public void Totals_StaysNull_WhenResultHasCostOnlyAndNoAssistantUsage()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","total_cost_usd":0.05}""");

        sut.Totals.Should().BeNull();
    }

    [Fact]
    public void Totals_PopulatesFromAccumulatedAssistantUsage_WhenResultHasNoCost()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":5000,"output_tokens":1200},"content":[{"type":"text","text":"hi"}]}}""");
        sut.Format("""{"type":"result","duration_ms":900}""");

        sut.Totals.Should().Be(new ClaudeRunTotals(5000, 1200));
    }

    [Fact]
    public void Totals_StaysNull_WhenNoCostAndNoAccumulatedTokens()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"result","duration_ms":900}""");

        sut.Totals.Should().BeNull();
    }

    [Fact]
    public void RunningTokens_AccumulateAcrossMultipleAssistantEvents()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":100,"output_tokens":40},"content":[{"type":"text","text":"a"}]}}""");
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":250,"output_tokens":60},"content":[{"type":"text","text":"b"}]}}""");

        sut.RunningInputTokens.Should().Be(350);
        sut.RunningOutputTokens.Should().Be(100);
    }

    [Fact]
    public void RunningTokens_StayZero_WhenAssistantHasNoUsage()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"hi"}]}}""");

        sut.RunningInputTokens.Should().Be(0);
        sut.RunningOutputTokens.Should().Be(0);
    }

    [Fact]
    public void RunningTokens_IgnoreNonNumericUsageFields()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":"oops","output_tokens":50},"content":[{"type":"text","text":"a"}]}}""");

        sut.RunningInputTokens.Should().Be(0);
        sut.RunningOutputTokens.Should().Be(50);
    }

    [Fact]
    public void ToolUseCount_ExposesRunningCount()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{}},{"type":"tool_use","name":"Edit","input":{}}]}}""");
        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Read","input":{}}]}}""");

        sut.ToolUseCount.Should().Be(3);
    }

    [Fact]
    public void OnStatus_FiresWithCleanedAssistantText_OnTextBlock()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"line1\nline2\t\tend"}]}}""");

        statuses.Should().ContainSingle().Which.Should().Be("line1 line2 end");
    }

    [Fact]
    public void OnStatus_FiresWithTruncatedAssistantText_WhenLong()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);
        var longText = new string('a', 200);
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\""
            + longText + "\"}]}}";

        sut.Format(line);

        statuses.Should().ContainSingle();
        statuses[0].Should().EndWith("…");
        statuses[0].Length.Should().Be(120 + 1);
    }

    [Fact]
    public void OnStatus_FiresWithToolName_OnToolUseBlock()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}}""");

        statuses.Should().ContainSingle().Which.Should().Be("Tool: Bash");
    }

    [Fact]
    public void OnStatus_FiresOncePerBlock_ForMultiBlockMessage()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"hi"},{"type":"tool_use","name":"Edit","input":{"file_path":"x.cs"}},{"type":"text","text":"bye"}]}}""");

        statuses.Should().Equal("hi", "Tool: Edit", "bye");
    }

    [Fact]
    public void OnStatus_DoesNotFire_OnResultEvent()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"result","duration_ms":1000,"total_cost_usd":0.01}""");

        statuses.Should().BeEmpty();
    }

    [Fact]
    public void OnStatus_DoesNotFire_OnToolResultEvent()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":"boom"}]}}""");

        statuses.Should().BeEmpty();
    }

    [Fact]
    public void OnStatus_DoesNotFire_WhenToolUseHasNoName()
    {
        var callbackHits = 0;
        var sut = new StreamJsonFormatter(_ => callbackHits++);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"tool_use","input":{"command":"ls"}}]}}""");

        callbackHits.Should().Be(0);
    }

    [Fact]
    public void OnStatus_DoesNotFire_WhenTextBlockIsBlank()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"   "}]}}""");

        statuses.Should().BeEmpty();
    }

    [Fact]
    public void Format_Returns_Null_For_TruncatedJson_And_Does_Not_Throw()
    {
        var sut = new StreamJsonFormatter();
        string? result = null;

        var act = () => { result = sut.Format("""{"type":"""); };

        act.Should().NotThrow();
        result.Should().BeNull();
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_Empty_Content_Array_Uses_Placeholder()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[]}]}}""";

        sut.Format(line).Should().Be("[error] (no detail)");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_NonObject_Array_Content_Uses_Placeholder()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[1,2,3]}]}}""";

        sut.Format(line).Should().Be("[error] (no detail)");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_NoTextType_In_Array_Uses_Placeholder()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[{"type":"image"}]}]}}""";

        sut.Format(line).Should().Be("[error] (no detail)");
    }

    [Fact]
    public void Format_Assistant_Text_Collapses_Multiple_Consecutive_Spaces_Inside_String()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"a  b   c  d"}]}}""";

        sut.Format(line).Should().Be("… a b c d");
    }

    [Fact]
    public void Format_Assistant_Text_Strips_Trailing_Whitespace_Only()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"hello   "}]}}""";

        sut.Format(line).Should().Be("… hello");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_Array_Item_Without_Text_Property_Uses_Placeholder()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[{"value":"data"}]}]}}""";

        sut.Format(line).Should().Be("[error] (no detail)");
    }

    [Fact]
    public void Format_Tool_Result_With_Error_Flag_MixedTypeArray_ReturnsTextFromFirstTextBlock()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"x","is_error":true,"content":[{"type":"image"},{"type":"text","text":"fallback"}]}]}}""";

        sut.Format(line).Should().Be("[error] fallback");
    }

    [Fact]
    public void Format_Result_With_String_Duration_Ms_Does_Not_Crash_And_Omits_Duration()
    {
        var sut = new StreamJsonFormatter();

        var result = sut.Format("""{"type":"result","duration_ms":"not a number","total_cost_usd":"abc"}""");

        result.Should().Be("done, 0 tool uses");
    }

    [Fact]
    public void RunningTokens_InputTokens_DefaultsToZero_WhenPropertyAbsent()
    {
        var sut = new StreamJsonFormatter();
        sut.Format("""{"type":"assistant","message":{"usage":{"output_tokens":50},"content":[{"type":"text","text":"a"}]}}""");

        sut.RunningInputTokens.Should().Be(0);
        sut.RunningOutputTokens.Should().Be(50);
    }

    [Fact]
    public void Format_WorksCorrectly_WhenOnStatusIsExplicitlyNull()
    {
        var sut = new StreamJsonFormatter(onStatus: null);
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"hello"}]}}""";
        string? result = null;

        var act = () => { result = sut.Format(line); };

        act.Should().NotThrow();
        result.Should().Be("… hello");
    }

    [Fact]
    public void OnStatus_OmitsTokenSuffix_WhenAssistantHasNoUsage()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"content":[{"type":"text","text":"hi"}]}}""");

        statuses.Should().ContainSingle().Which.Should().Be("hi");
    }

    [Fact]
    public void OnStatus_AppendsTokenSuffix_WhenRunningTokensNonZero()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":1234,"output_tokens":340},"content":[{"type":"text","text":"hi"}]}}""");

        statuses.Should().ContainSingle().Which.Should().Be("hi — 1.2k↑ 340↓");
    }

    [Fact]
    public void OnStatus_AppendsTokenSuffix_OnToolUseBlock()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":1234,"output_tokens":340},"content":[{"type":"tool_use","name":"Edit","input":{}}]}}""");

        statuses.Should().ContainSingle().Which.Should().Be("Tool: Edit — 1.2k↑ 340↓");
    }

    [Fact]
    public void OnStatus_TokenSuffix_AccumulatesAcrossEvents()
    {
        var statuses = new List<string>();
        var sut = new StreamJsonFormatter(statuses.Add);

        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":600,"output_tokens":200},"content":[{"type":"text","text":"a"}]}}""");
        sut.Format("""{"type":"assistant","message":{"usage":{"input_tokens":600,"output_tokens":200},"content":[{"type":"text","text":"b"}]}}""");

        statuses.Should().Equal(
            "a — 600↑ 200↓",
            "b — 1.2k↑ 400↓");
    }
}
