using smash_dates.Services.Import;

namespace smash_dates.UnitTests.Services.Import;

public sealed class CsvParserTests
{
    [Fact]
    public void Parse_SimpleRows_MapsByHeader()
    {
        var doc = CsvParser.Parse("name,gender\nRiverside 1st,Mens\nRiverside Ladies,Ladies");

        doc.Headers.Should().Equal("name", "gender");
        doc.Rows.Should().HaveCount(2);
        doc.Rows[0].Get("name").Should().Be("Riverside 1st");
        doc.Rows[0].Get("gender").Should().Be("Mens");
        doc.Rows[1].Get("gender").Should().Be("Ladies");
    }

    [Fact]
    public void Parse_QuotedFieldWithCommaAndNewline_KeptLiteral()
    {
        var doc = CsvParser.Parse("name,notes\n\"Smith, John\",\"line1\nline2\"");

        doc.Rows.Should().HaveCount(1);
        doc.Rows[0].Get("name").Should().Be("Smith, John");
        doc.Rows[0].Get("notes").Should().Be("line1\nline2");
    }

    [Fact]
    public void Parse_EscapedDoubleQuotes_Unescaped()
    {
        var doc = CsvParser.Parse("phrase\n\"He said \"\"hi\"\"\"");

        doc.Rows[0].Get("phrase").Should().Be("He said \"hi\"");
    }

    [Fact]
    public void Parse_StripsBomAndHandlesCrLf()
    {
        var doc = CsvParser.Parse("﻿name,capacity\r\nHall,2\r\n");

        doc.Headers.Should().Equal("name", "capacity");
        doc.Rows.Should().HaveCount(1);
        doc.Rows[0].Get("name").Should().Be("Hall");
        doc.Rows[0].Get("capacity").Should().Be("2");
    }

    [Fact]
    public void Parse_SkipsBlankLines_AndTrimsValuesAndHeaders()
    {
        var doc = CsvParser.Parse(" Name , Gender \n\n Riverside , Mens \n\n");

        doc.Headers.Should().Equal("Name", "Gender"); // original case preserved; lookup is case-insensitive
        doc.Rows.Should().HaveCount(1);
        doc.Rows[0].Get("name").Should().Be("Riverside");
        doc.Rows[0].Get("gender").Should().Be("Mens");
    }

    [Fact]
    public void Parse_HeaderLookupIsCaseInsensitive()
    {
        var doc = CsvParser.Parse("Name,ShortCode\nThames Valley,TVB");

        doc.Rows[0].Get("name").Should().Be("Thames Valley");
        doc.Rows[0].Get("SHORTCODE").Should().Be("TVB");
    }

    [Fact]
    public void Parse_MissingTrailingColumn_YieldsEmptyString()
    {
        var doc = CsvParser.Parse("name,contactEmail,notes\nAcme,acme@test");

        doc.Rows[0].Get("notes").Should().Be("");
    }

    [Fact]
    public void Parse_RowLineNumbers_PointAtSourceLine()
    {
        var doc = CsvParser.Parse("name\nA\nB");

        doc.Rows[0].LineNumber.Should().Be(2); // header is line 1
        doc.Rows[1].LineNumber.Should().Be(3);
    }

    [Fact]
    public void Parse_EmptyInput_HasNoRows()
    {
        var doc = CsvParser.Parse("");

        doc.Headers.Should().BeEmpty();
        doc.Rows.Should().BeEmpty();
    }
}
