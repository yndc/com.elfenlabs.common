using NUnit.Framework; // Or your preferred testing framework
using Unity.Collections;
using Elfenlabs.String; // For GetUnsafePtr

// Assuming MarkupParser, Element, ElementAttribute, Range, MarkupRuleFlag, OpenElementInfo
// are defined as in the csharp_markup_parser Canvas.

public class MarkupParserTests
{
    private void PrintResults(string input, NativeList<Element> elements, NativeList<ElementAttribute> attributes)
    {
        UnityEngine.Debug.Log($"--- Test Input: \"{input}\" ---");
        UnityEngine.Debug.Log($"Found {elements.Length} elements:");
        for (int i = 0; i < elements.Length; i++)
        {
            var e = elements[i];
            string tagName = e.TagName.IsEmpty ? "N/A" : input.Substring(e.TagName.Start, e.TagName.Length);
            string content = e.Content.IsEmpty ? "N/A" : input.Substring(e.Content.Start, e.Content.Length);
            string fullTag = e.FullOpeningTag.IsEmpty ? "N/A" : input.Substring(e.FullOpeningTag.Start, e.FullOpeningTag.Length);
            UnityEngine.Debug.Log($"  Element {i}: TagName='{tagName}' (Range:{e.TagName}), Content='{content}' (Range:{e.Content}), FullOpeningTag='{fullTag}' (Range:{e.FullOpeningTag})");
        }

        UnityEngine.Debug.Log($"Found {attributes.Length} attributes:");
        for (int i = 0; i < attributes.Length; i++)
        {
            var a = attributes[i];
            string key = a.Key.IsEmpty ? "N/A" : input.Substring(a.Key.Start, a.Key.Length);
            string val = a.Value.IsEmpty ? "N/A (valueless)" : input.Substring(a.Value.Start, a.Value.Length);
            UnityEngine.Debug.Log($"  Attribute {i}: ElementIndex={a.ElementIndex}, Key='{key}' (Range:{a.Key}), Value='{val}' (Range:{a.Value})");
        }
        UnityEngine.Debug.Log($"------------------------------------");
    }

    private void AssertRange(Range actual, int expectedStart, int expectedLength, string context)
    {
        Assert.AreEqual(expectedStart, actual.Start, $"{context} - Start mismatch.");
        Assert.AreEqual(expectedLength, actual.Length, $"{context} - Length mismatch.");
    }

    private void AssertSubstring(string expected, string source, Range range)
    {
        Assert.IsTrue(range.Start + range.Length <= source.Length, "Range should not exceed source length.");
        string actual = source.Substring(range.Start, range.Length);
        Assert.AreEqual(expected, actual, $"Extracted string does not match expected value. Expected: '{expected}', Actual: '{actual}'");
    }

    [Test]
    public void Parse_EmptyString_ReturnsEmptyLists()
    {
        string input = "";
        // Use the new string overload
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);

        Assert.AreEqual(0, elements.Length, "Elements count should be 0 for empty string.");
        Assert.AreEqual(0, attributes.Length, "Attributes count should be 0 for empty string.");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_SimpleTag_CorrectElement()
    {
        string input = "<a>content</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        AssertRange(elements[0].TagName, 1, 1, "Element 0 TagName"); // "a"
        Assert.AreEqual("a", input.Substring(elements[0].TagName.Start, elements[0].TagName.Length));
        AssertRange(elements[0].FullOpeningTag, 0, 3, "Element 0 FullOpeningTag"); // "<a>"
        AssertRange(elements[0].Content, 3, 7, "Element 0 Content"); // "content"
        Assert.AreEqual("content", input.Substring(elements[0].Content.Start, elements[0].Content.Length));

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_NestedTags_CorrectElements()
    {
        string input = "<a><b>content</b></a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(2, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        // Outer <a> tag
        AssertRange(elements[0].TagName, 1, 1, "Element 0 (a) TagName"); // "a"
        AssertRange(elements[0].FullOpeningTag, 0, 3, "Element 0 (a) FullOpeningTag"); // "<a>"
        AssertRange(elements[0].Content, 3, 14, "Element 0 (a) Content"); // "<b>content</b>"

        // Inner <b> tag
        AssertRange(elements[1].TagName, 4, 1, "Element 1 (b) TagName"); // "b"
        AssertRange(elements[1].FullOpeningTag, 3, 3, "Element 1 (b) FullOpeningTag"); // "<b>"
        AssertRange(elements[1].Content, 6, 7, "Element 1 (b) Content"); // "content"

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_SelfClosingTag_CorrectElement()
    {
        string input = "<img src=\"url\"/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(1, attributes.Length);

        AssertRange(elements[0].TagName, 1, 3, "Element 0 TagName"); // "img"
        AssertRange(elements[0].FullOpeningTag, 0, input.Length, "Element 0 FullOpeningTag");
        Assert.IsTrue(elements[0].Content.IsEmpty, "Self-closing tag content should be empty.");

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertRange(attributes[0].Key, 5, 3, "Attribute 0 Key"); // "src"
        AssertRange(attributes[0].Value, 10, 3, "Attribute 0 Value"); // "url"

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_TagWithAttributes_CorrectAttributes()
    {
        string input = "<a href='link' id=\"myId\">text</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(2, attributes.Length);

        AssertRange(elements[0].TagName, 1, 1, "Element 0 TagName"); // "a"
        AssertRange(elements[0].FullOpeningTag, 0, 24, "Element 0 FullOpeningTag");
        AssertRange(elements[0].Content, 24, 4, "Element 0 Content"); // "text"

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertRange(attributes[0].Key, 3, 4, "Attribute 0 Key"); // "href"
        AssertRange(attributes[0].Value, 9, 4, "Attribute 0 Value"); // "link"

        Assert.AreEqual(0, attributes[1].ElementIndex);
        AssertRange(attributes[1].Key, 15, 2, "Attribute 1 Key"); // "id"
        AssertRange(attributes[1].Value, 19, 4, "Attribute 1 Value"); // "myId"

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_Allowed()
    {
        string input = "<input disabled checked/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowValuelessAttribute);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(2, attributes.Length);

        AssertRange(elements[0].TagName, 1, 5, "Element 0 TagName"); // "input"

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertRange(attributes[0].Key, 7, 8, "Attribute 0 Key"); // "disabled"
        Assert.IsTrue(attributes[0].Value.IsEmpty, "Valueless attribute 'disabled' should have empty value.");

        Assert.AreEqual(0, attributes[1].ElementIndex);
        AssertRange(attributes[1].Key, 16, 7, "Attribute 1 Key"); // "checked"
        Assert.IsTrue(attributes[1].Value.IsEmpty, "Valueless attribute 'checked' should have empty value.");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_NotAllowed()
    {
        string input = "<input disabled/>"; // 'disabled' is valueless
        // Rule does NOT include AllowValuelessAttribute
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.None);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        // 'disabled' should NOT be parsed as an attribute if valueless attributes are not allowed
        Assert.AreEqual(0, attributes.Length, "Attributes count should be 0 when valueless are not allowed and attribute has no value.");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_ElementValue_Allowed()
    {
        string input = "<color=\"red\">text</color>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.AllowElementValue);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(0, attributes.Length);

        AssertSubstring("color", input, elements[0].TagName);
        AssertSubstring("red", input, elements[0].Value);
        AssertSubstring("text", input, elements[0].Content);
        AssertSubstring("<color=\"red\">", input, elements[0].FullOpeningTag);

        elements.Dispose();
        attributes.Dispose();
    }


    [Test]
    public void Parse_ElementValue_NotAllowed()
    {
        string input = "<color=\"red\">text</color>";
        // Rule does NOT include AllowElementValue
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.None);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(0, attributes.Length, "Attributes count should be 0 when element value is not allowed.");

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_UnclosedTag_ContentToEnd()
    {
        string input = "<a><b>unclosed content";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(2, elements.Length); // <a> and <b>

        // Check <b>
        AssertRange(elements[1].TagName, 4, 1, "Element b TagName"); // "b"
        AssertRange(elements[1].Content, 6, input.Length - 6, "Element b Content"); // "unclosed content"

        // Check <a>
        AssertRange(elements[0].TagName, 1, 1, "Element a TagName"); // "a"
        AssertRange(elements[0].Content, 3, input.Length - 3, "Element a Content"); // "<b>unclosed content"

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_MismatchedClosingTag_IgnoresMismatchAndClosesOuter()
    {
        string input = "<a><b>content</c></a>"; // </b> expected, </a> closes <a>
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(2, elements.Length); // Expecting <a> and <b>

        // <b> is opened but not properly closed by </c>. Its content runs until </c>
        AssertRange(elements[1].TagName, 4, 1, "Element b TagName");
        AssertRange(elements[1].Content, 6, 7, "Content of b"); // "content"

        // <a> is properly closed
        AssertRange(elements[0].TagName, 1, 1, "Element a TagName");
        AssertRange(elements[0].Content, 3, input.Length - 3 - 4, "Content of a"); // "<b>content</c>"

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_AttributeWithNoValue_AndNotAllowed_SkipsAttribute()
    {
        string input = "<tag attr1 valueless attr2=\"value\"/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes, MarkupRuleFlag.None); // Valueless NOT allowed
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual(1, attributes.Length); // Only attr2 should be parsed

        Assert.AreEqual(0, attributes[0].ElementIndex);
        AssertSubstring("attr2", input, attributes[0].Key);
        AssertSubstring("value", input, attributes[0].Value);

        elements.Dispose();
        attributes.Dispose();
    }

    [Test]
    public void Parse_MalformedTagStart_Skips()
    {
        string input = "<<ignore><a>text</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var elements, out var attributes);
        PrintResults(input, elements, attributes);

        Assert.AreEqual(1, elements.Length); // Only <a> should be parsed
        AssertRange(elements[0].TagName, 10, 1, "Element 0 TagName"); // "a"
        AssertRange(elements[0].Content, 12, 4, "Element 0 Content"); // "text"

        elements.Dispose();
        attributes.Dispose();
    }
}
