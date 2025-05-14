using NUnit.Framework;
using Unity.Collections;
using Elfenlabs.String; // Your namespace
using System; // For ArgumentException

using Range = Elfenlabs.String.Range; // Assuming Range is defined in your namespace

public class MarkupParserTests
{
    // Helper method to extract string from Range for readability in assertions
    private string GetString(string original, Range range)
    {
        if (range.IsEmpty || range.Start < 0 || original == null || range.Start + range.Length > original.Length) return "[Invalid Range In Test]";
        return original.Substring(range.Start, range.Length);
    }

    private void AssertSubstring(string expectedSubstring, string originalString, Range range, string context = "")
    {
        if (range.IsEmpty)
        {
            Assert.IsTrue(string.IsNullOrEmpty(expectedSubstring), $"{context} - Expected empty substring for empty range, but got '{expectedSubstring}'.");
            return;
        }
        Assert.IsFalse(range.IsEmpty, $"{context} - Range should not be empty if expecting substring '{expectedSubstring}'.");
        Assert.IsTrue(range.Start >= 0 && range.Start <= originalString.Length, $"{context} - Range start {range.Start} is out of bounds for string length {originalString.Length}.");
        Assert.IsTrue(range.Start + range.Length <= originalString.Length, $"{context} - Range end {range.Start + range.Length} is out of bounds for string length {originalString.Length}.");

        string actualSubstring = originalString.Substring(range.Start, range.Length);
        Assert.AreEqual(expectedSubstring, actualSubstring, $"{context} - Substring mismatch.");
    }


    [Test]
    public void Parse_EmptyString_ReturnsEmptyLists()
    {
        string input = "";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);

        Assert.AreEqual(0, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_SimpleTag_CorrectElement()
    {
        string input = "<a>content</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        AssertSubstring("a", input, elements[0].TagName, "Element 0 TagName");
        AssertSubstring("<a>", input, elements[0].FullOpeningTag, "Element 0 FullOpeningTag");
        AssertSubstring("content", input, elements[0].Content, "Element 0 Content");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_NestedTags_CorrectElements()
    {
        string input = "<a><b>content</b></a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);

        Assert.AreEqual(2, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        AssertSubstring("a", input, elements[0].TagName, "Element 0 (a) TagName");
        AssertSubstring("<a>", input, elements[0].FullOpeningTag, "Element 0 (a) FullOpeningTag");
        AssertSubstring("<b>content</b>", input, elements[0].Content, "Element 0 (a) Content");

        AssertSubstring("b", input, elements[1].TagName, "Element 1 (b) TagName");
        AssertSubstring("<b>", input, elements[1].FullOpeningTag, "Element 1 (b) FullOpeningTag");
        AssertSubstring("content", input, elements[1].Content, "Element 1 (b) Content");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_SelfClosingTag_Allowed_CorrectElement()
    {
        string input = "<img src=\"url\"/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowEmptyTag | MarkupRuleFlag.AllowValuelessAttribute);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(1, attributes.Length);

        AssertSubstring("img", input, elements[0].TagName, "Element 0 TagName");
        AssertSubstring(input, input, elements[0].FullOpeningTag, "Element 0 FullOpeningTag");
        Assert.IsTrue(elements[0].Content.IsEmpty, "Self-closing tag content should be empty.");

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertSubstring("src", input, attributes[0].Key, "Attribute 0 Key");
        AssertSubstring("url", input, attributes[0].Value, "Attribute 0 Value");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_SelfClosingTag_NotAllowed_Throws()
    {
        string input = "<img/>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.None);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Self-closing tag 'img'", ex.Message);
        StringAssert.Contains("not allowed", ex.Message);
        Assert.IsInstanceOf<SelfClosingTagNotAllowedException>(ex.InnerException);
    }


    [Test]
    public void Parse_TagWithAttributes_CorrectAttributes()
    {
        string input = "<a href='link' id=\"myId\">text</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(2, attributes.Length);

        AssertSubstring("a", input, elements[0].TagName);
        AssertSubstring("<a href='link' id=\"myId\">", input, elements[0].FullOpeningTag);
        AssertSubstring("text", input, elements[0].Content);

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertSubstring("href", input, attributes[0].Key);
        AssertSubstring("link", input, attributes[0].Value);

        Assert.AreEqual(0, attributes[1].ElementIndex);
        AssertSubstring("id", input, attributes[1].Key);
        AssertSubstring("myId", input, attributes[1].Value);

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_Allowed()
    {
        string input = "<input disabled checked/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(2, attributes.Length);
        AssertSubstring("input", input, elements[0].TagName);

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertSubstring("disabled", input, attributes[0].Key);
        Assert.IsTrue(attributes[0].Value.IsEmpty);

        Assert.AreEqual(0, attributes[1].ElementIndex);
        AssertSubstring("checked", input, attributes[1].Key);
        Assert.IsTrue(attributes[1].Value.IsEmpty);

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_NotAllowed_Throws()
    {
        string input = "<input disabled/>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowEmptyTag); // Valueless NOT allowed
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Valueless attribute 'disabled'", ex.Message);
        StringAssert.Contains("not allowed for tag 'input'", ex.Message);
        Assert.IsInstanceOf<ValuelessAttributeNotAllowedException>(ex.InnerException);
    }

    [Test]
    public void Parse_ElementValue_Allowed()
    {
        string input = "<color=\"red\">text</color>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowElementValue);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(0, attributes.Length, "Element value should not create an attribute entry.");

        AssertSubstring("color", input, elements[0].TagName);
        AssertSubstring("red", input, elements[0].Value, "Element Value");
        AssertSubstring("text", input, elements[0].Content);
        AssertSubstring("<color=\"red\">", input, elements[0].FullOpeningTag);

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ElementValue_NotAllowed_Throws()
    {
        string input = "<color=\"red\">text</color>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.None); // ElementValue NOT allowed
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Element value syntax not allowed", ex.Message);
        StringAssert.Contains("tag 'color'", ex.Message);
        Assert.IsInstanceOf<ElementValueNotAllowedException>(ex.InnerException);
    }

    [Test]
    public void Parse_UnclosedTag_Throws()
    {
        string input = "<a><b>unclosed content";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Unclosed tag 'b'", ex.Message); // Innermost unclosed tag
        Assert.IsInstanceOf<UnclosedTagException>(ex.InnerException);
        var specificEx = (UnclosedTagException)ex.InnerException;
        AssertSubstring("b", input, specificEx.ContextRange); // TagName
    }

    [Test]
    public void Parse_MismatchedClosingTag_Throws()
    {
        string input = "<a><b>content</c></a>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Mismatched closing tag", ex.Message);
        Assert.IsInstanceOf<MismatchedClosingTagException>(ex.InnerException);
        var specificEx = (MismatchedClosingTagException)ex.InnerException;
        AssertSubstring("c", input, specificEx.ActualTagNameRange);
        AssertSubstring("b", input, specificEx.ExpectedTagNameRange);
    }

    [Test]
    public void Parse_UnterminatedOpeningTag_Throws()
    {
        string input = "<a href='link'";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Unterminated opening for tag 'a'", ex.Message);
        Assert.IsInstanceOf<UnterminatedTagException>(ex.InnerException);
    }

    [Test]
    public void Parse_EmptyTagName_Throws()
    {
        string input = "<>text</>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Empty tag name at index 1. Found '>'", ex.Message); // Updated expected message
        Assert.IsInstanceOf<EmptyTagNameException>(ex.InnerException);
    }

    [Test]
    public void Parse_MultipleElementValues_Throws()
    {
        string input = "<tag=val1 =val2>text</tag>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowElementValue);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Multiple element values", ex.Message);
        StringAssert.Contains("tag 'tag'", ex.Message);
        Assert.IsInstanceOf<MultipleElementValuesException>(ex.InnerException);
    }

    [Test]
    public void Parse_UnterminatedAttributeValue_Throws()
    {
        string input = "<a href=\"link>text</a>"; // Missing closing quote
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Unterminated attribute for attribute 'href'", ex.Message); // Corrected context
        Assert.IsInstanceOf<UnterminatedTagException>(ex.InnerException);
        var specificEx = (UnterminatedTagException)ex.InnerException;
        Assert.AreEqual(UnterminatedTagException.TagType.Attribute, specificEx.TypeOfTag);
        AssertSubstring("href", input, specificEx.ContextRange); // Context is attrName
    }

    [Test]
    public void Parse_UnexpectedClosingTag_Throws()
    {
        string input = "text</closed>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Mismatched closing tag", ex.Message);
        StringAssert.Contains("got '</closed>'", ex.Message);
        StringAssert.Contains("Expected '[NO TAG OPEN]'", ex.Message); // Corrected expected
        Assert.IsInstanceOf<MismatchedClosingTagException>(ex.InnerException);
    }

    [Test]
    public void Parse_InvalidCharInTag_Throws()
    {
        string input = "<tag !attr='val'>text</tag>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
            elements.Dispose();
            attributes.Dispose();
        });
        StringAssert.Contains("Invalid character '!' found within tag 'tag'", ex.Message);
        Assert.IsInstanceOf<InvalidCharacterInTagException>(ex.InnerException);
    }
}
