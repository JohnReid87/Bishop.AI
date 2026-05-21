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

    [Fact]
    public void Format_Tool_Use_Bash_Uses_Colon_With_Command_Subject()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash","input":{"command":"dotnet test"}}]}}""";

        sut.Format(line).Should().Be("→ Bash: dotnet test");
    }

    [Fact]
    public void Format_Tool_Use_Edit_Uses_Space_With_File_Path()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"src/Foo.cs"}}]}}""";

        sut.Format(line).Should().Be("→ Edit src/Foo.cs");
    }

    [Fact]
    public void Format_Tool_Use_Grep_Uses_Colon_With_Pattern()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Grep","input":{"pattern":"TODO"}}]}}""";

        sut.Format(line).Should().Be("→ Grep: TODO");
    }

    [Fact]
    public void Format_Tool_Use_Without_Recognised_Input_Field_Omits_Subject()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"TodoWrite","input":{"todos":[]}}]}}""";

        sut.Format(line).Should().Be("→ TodoWrite");
    }

    [Fact]
    public void Format_Tool_Use_Subject_Is_Truncated()
    {
        var sut = new StreamJsonFormatter();
        var longCmd = new string('x', 200);
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Bash\",\"input\":{\"command\":\""
            + longCmd + "\"}}]}}";

        var output = sut.Format(line);

        output.Should().NotBeNull();
        output!.Should().StartWith("→ Bash: ");
        output.Should().EndWith("…");
    }

    [Fact]
    public void Format_Multiple_Content_Blocks_Produces_One_Line_Per_Block()
    {
        var sut = new StreamJsonFormatter();
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"hi"},{"type":"tool_use","name":"Bash","input":{"command":"ls"}}]}}""";

        var output = sut.Format(line);

        output.Should().NotBeNull();
        var lines = output!.Split(Environment.NewLine);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("… hi");
        lines[1].Should().Be("→ Bash: ls");
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
}
