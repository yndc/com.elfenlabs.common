using NUnit.Framework;
using Unity.Collections;
using Elfenlabs.String; // Your namespace
using System;

using Range = Elfenlabs.String.Range; // Assuming you have a Range struct defined in your project

public class MarkupParserTests
{
    // Helper method to extract string from Range for readability in assertions
    private string GetString(string original, Range range)
    {
        if (original == null) return "[NULL ORIGINAL STRING IN TEST]";
        if (range.IsEmpty || range.Start < 0 || range.Start + range.Length > original.Length)
        {
            // Provide more context if range is invalid for a non-empty original string
            if (!range.IsEmpty && original.Length > 0)
            {
                return $"[Invalid Range ({range.Start},{range.Length}) for string of length {original.Length}]";
            }
            return string.Empty; // Default for truly empty or invalid scenarios
        }
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
        Assert.IsTrue(range.Start >= 0 && range.Start <= originalString.Length, $"{context} - Range start {range.Start} out of bounds for '{originalString}'. Length: {originalString.Length}");
        Assert.IsTrue(range.Start + range.Length <= originalString.Length, $"{context} - Range end {range.Start + range.Length} out of bounds for '{originalString}'. Length: {originalString.Length}");

        string actualSubstring = originalString.Substring(range.Start, range.Length);
        Assert.AreEqual(expectedSubstring, actualSubstring, $"{context} - Substring mismatch.");
    }

    private void PrintResults(string input, TextMarkupFeatures features)
    {
        UnityEngine.Debug.Log($"--- Test Input: \"{input}\" ---");
        UnityEngine.Debug.Log($"Found {features.Elements.Length} elements:");
        for (int i = 0; i < features.Elements.Length; i++)
        {
            var e = features.Elements[i];
            string tagName = GetString(input, e.TagName);
            string tagValue = GetString(input, e.TagValue);
            UnityEngine.Debug.Log($"  Element {i}: TagName='{tagName}' (Range:{e.TagName}), TagValue='{tagValue}' (Range:{e.TagValue})");
        }

        UnityEngine.Debug.Log($"Found {features.Attributes.Length} attributes:");
        for (int i = 0; i < features.Attributes.Length; i++)
        {
            var a = features.Attributes[i];
            string key = GetString(input, a.Key);
            string val = GetString(input, a.Value);
            UnityEngine.Debug.Log($"  Attribute {i}: OwnerElementIndex={a.OwnerElementIndex}, Key='{key}' (Range:{a.Key}), Value='{val}' (Range:{a.Value})");
        }

        UnityEngine.Debug.Log($"Found {features.Contents.Length} content entries:");
        for (int i = 0; i < features.Contents.Length; i++)
        {
            var c = features.Contents[i];
            if (c.IsText)
            {
                UnityEngine.Debug.Log($"  Content {i}: OwnerElementIdx={c.OwnerElementIndex}, Type=Text, Text='{GetString(input, c.Content)}' (Range:{c.Content})");
            }
            else
            {
                UnityEngine.Debug.Log($"  Content {i}: OwnerElementIdx={c.OwnerElementIndex}, Type=Element, ChildElementIdx={c.ContentElementIndex}");
            }
        }
        UnityEngine.Debug.Log($"------------------------------------");
    }


    [Test]
    public void Parse_EmptyString_ReturnsEmptyLists()
    {
        string input = "";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        Assert.AreEqual(0, features.Elements.Length);
        Assert.AreEqual(0, features.Attributes.Length);
        Assert.AreEqual(0, features.Contents.Length);
        features.Dispose();
    }

    [Test]
    public void Parse_SimpleTag_CorrectOutput()
    {
        string input = "<a>content</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        // PrintResults(input, features);

        Assert.AreEqual(1, features.Elements.Length, "Elements count");
        Assert.AreEqual(0, features.Attributes.Length, "Attributes count");
        Assert.AreEqual(1, features.Contents.Length, "Contents count");

        AssertSubstring("a", input, features.Elements[0].TagName, "Element 0 TagName");
        Assert.IsTrue(features.Elements[0].TagValue.IsEmpty, "Element 0 TagValue should be empty");

        Assert.AreEqual(0, features.Contents[0].OwnerElementIndex, "Content 0 Owner");
        Assert.IsTrue(features.Contents[0].IsText, "Content 0 IsText");
        AssertSubstring("content", input, features.Contents[0].Content, "Content 0 Text");

        features.Dispose();
    }

    [Test]
    public void Parse_NestedTags_CorrectOutput()
    {
        string input = "<a><b>content</b>text after b</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        // PrintResults(input, features);

        Assert.AreEqual(2, features.Elements.Length, "Elements count"); // <a> and <b>
        Assert.AreEqual(0, features.Attributes.Length, "Attributes count");
        Assert.AreEqual(3, features.Contents.Length, "Contents count");

        // Element 0: <a>
        AssertSubstring("a", input, features.Elements[0].TagName);
        // Element 1: <b>
        AssertSubstring("b", input, features.Elements[1].TagName);

        // Contents
        // Content 0: <b> (child of <a>, OwnerElementIndex=0, ContentElementIndex=1)
        Assert.AreEqual(0, features.Contents[0].OwnerElementIndex, "Content 0 Owner (a)");
        Assert.AreEqual(1, features.Contents[0].ContentElementIndex, "Content 0 Child Index (b)");
        Assert.IsTrue(features.Contents[0].IsNestedElement, "Content 0 IsNestedElement");

        // Content 1: "content" (child of <b>, OwnerElementIndex=1, IsText)
        Assert.AreEqual(1, features.Contents[1].OwnerElementIndex, "Content 1 Owner (b)");
        Assert.IsTrue(features.Contents[1].IsText, "Content 1 IsText");
        AssertSubstring("content", input, features.Contents[1].Content, "Content 1 Text");

        // Content 2: "text after b" (child of <a>, OwnerElementIndex=0, IsText)
        Assert.AreEqual(0, features.Contents[2].OwnerElementIndex, "Content 2 Owner (a)");
        Assert.IsTrue(features.Contents[2].IsText, "Content 2 IsText");
        AssertSubstring("text after b", input, features.Contents[2].Content, "Content 2 Text");

        features.Dispose();
    }

    [Test]
    public void Parse_SelfClosingTag_Allowed_CorrectOutput()
    {
        string input = "<a><img src=\"url\"/>text</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.AllowEmptyTag | MarkupRuleFlag.AllowValuelessAttribute);
        // PrintResults(input, features);

        Assert.AreEqual(2, features.Elements.Length); // <a> and <img>
        Assert.AreEqual(1, features.Attributes.Length); // src="url"

        AssertSubstring("a", input, features.Elements[0].TagName);
        AssertSubstring("img", input, features.Elements[1].TagName);
        Assert.IsTrue(features.Elements[1].TagValue.IsEmpty, "Self-closing img tag should have empty TagValue");


        Assert.AreEqual(1, features.Attributes[0].OwnerElementIndex, "Attribute owner is img (index 1)");
        AssertSubstring("src", input, features.Attributes[0].Key);
        AssertSubstring("url", input, features.Attributes[0].Value);

        Assert.AreEqual(2, features.Contents.Length);
        // Content 0: <img> (child of <a>)
        Assert.AreEqual(0, features.Contents[0].OwnerElementIndex);
        Assert.AreEqual(1, features.Contents[0].ContentElementIndex);
        Assert.IsTrue(features.Contents[0].IsNestedElement);
        // Content 1: "text" (child of <a>)
        Assert.AreEqual(0, features.Contents[1].OwnerElementIndex);
        Assert.IsTrue(features.Contents[1].IsText);
        AssertSubstring("text", input, features.Contents[1].Content);

        features.Dispose();
    }

    [Test]
    public void Parse_SelfClosingTag_NotAllowed_Throws()
    {
        string input = "<img/>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.None);
            features.Dispose();
        });
        StringAssert.Contains("Self-closing tag 'img'", ex.Message);
        StringAssert.Contains("not allowed", ex.Message);
        Assert.IsInstanceOf<SelfClosingTagNotAllowedException>(ex.InnerException);
    }


    [Test]
    public void Parse_TagWithAttributes_CorrectOutput()
    {
        string input = "<a href='link' id=\"myId\">text</a>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        // PrintResults(input, features);

        Assert.AreEqual(1, features.Elements.Length);
        Assert.AreEqual(2, features.Attributes.Length);
        Assert.AreEqual(1, features.Contents.Length);

        AssertSubstring("a", input, features.Elements[0].TagName);
        Assert.IsTrue(features.Elements[0].TagValue.IsEmpty);

        Assert.AreEqual(0, features.Attributes[0].OwnerElementIndex);
        AssertSubstring("href", input, features.Attributes[0].Key);
        AssertSubstring("link", input, features.Attributes[0].Value);

        Assert.AreEqual(0, features.Attributes[1].OwnerElementIndex);
        AssertSubstring("id", input, features.Attributes[1].Key);
        AssertSubstring("myId", input, features.Attributes[1].Value);

        Assert.AreEqual(0, features.Contents[0].OwnerElementIndex);
        Assert.IsTrue(features.Contents[0].IsText);
        AssertSubstring("text", input, features.Contents[0].Content);

        features.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_Allowed()
    {
        string input = "<input disabled checked/>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag);
        // PrintResults(input, features);

        Assert.AreEqual(1, features.Elements.Length);
        Assert.AreEqual(2, features.Attributes.Length);
        Assert.AreEqual(0, features.Contents.Length, "Self-closing tag should have no direct text content entries");

        AssertSubstring("input", input, features.Elements[0].TagName);

        Assert.AreEqual(0, features.Attributes[0].OwnerElementIndex);
        AssertSubstring("disabled", input, features.Attributes[0].Key);
        Assert.IsTrue(features.Attributes[0].Value.IsEmpty);

        Assert.AreEqual(0, features.Attributes[1].OwnerElementIndex);
        AssertSubstring("checked", input, features.Attributes[1].Key);
        Assert.IsTrue(features.Attributes[1].Value.IsEmpty);

        features.Dispose();
    }

    [Test]
    public void Parse_ValuelessAttribute_NotAllowed_Throws()
    {
        string input = "<input disabled/>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.AllowEmptyTag); // Valueless NOT allowed
            features.Dispose();
        });
        StringAssert.Contains("Valueless attribute 'disabled'", ex.Message);
        StringAssert.Contains("not allowed for tag 'input'", ex.Message);
        Assert.IsInstanceOf<ValuelessAttributeNotAllowedException>(ex.InnerException);
    }

    [Test]
    public void Parse_ElementValue_Allowed_CorrectOutput()
    {
        string input = "<color=\"red\">text</color>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.AllowElementValue);
        // PrintResults(input, features);

        Assert.AreEqual(1, features.Elements.Length);
        Assert.AreEqual(0, features.Attributes.Length, "Element value should not create an attribute entry.");
        Assert.AreEqual(1, features.Contents.Length);

        AssertSubstring("color", input, features.Elements[0].TagName);
        AssertSubstring("red", input, features.Elements[0].TagValue, "Element Value");

        Assert.AreEqual(0, features.Contents[0].OwnerElementIndex);
        Assert.IsTrue(features.Contents[0].IsText);
        AssertSubstring("text", input, features.Contents[0].Content);

        features.Dispose();
    }

    [Test]
    public void Parse_ElementValue_NotAllowed_Throws()
    {
        string input = "<color=\"red\">text</color>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.None); // ElementValue NOT allowed
            features.Dispose();
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
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
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
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
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
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
        });
        StringAssert.Contains("Unterminated opening tag for tag 'a'", ex.Message);
        Assert.IsInstanceOf<UnterminatedTagException>(ex.InnerException);
    }

    [Test]
    public void Parse_EmptyTagName_Throws()
    {
        string input = "<>text</>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
        });
        StringAssert.Contains("Empty tag name at index 1. Found '>'", ex.Message);
        Assert.IsInstanceOf<EmptyTagNameException>(ex.InnerException);
    }

    [Test]
    public void Parse_MultipleElementValues_Throws()
    {
        string input = "<tag=val1 =val2>text</tag>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features, MarkupRuleFlag.AllowElementValue);
            features.Dispose();
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
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
        });
        StringAssert.Contains("Unterminated attribute tag for attribute 'href'", ex.Message);
        Assert.IsInstanceOf<UnterminatedTagException>(ex.InnerException);
        var specificEx = (UnterminatedTagException)ex.InnerException;
        Assert.AreEqual(UnterminatedTagException.TagType.Attribute, specificEx.TypeOfTag);
        AssertSubstring("href", input, specificEx.ContextRange);
    }

    [Test]
    public void Parse_UnexpectedClosingTag_Throws()
    {
        string input = "text</closed>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
        });
        StringAssert.Contains("Mismatched closing tag", ex.Message);
        StringAssert.Contains("got '</closed>'", ex.Message);
        StringAssert.Contains("Expected '[NO TAG OPEN]'", ex.Message);
        Assert.IsInstanceOf<MismatchedClosingTagException>(ex.InnerException);
    }

    [Test]
    public void Parse_InvalidCharInTag_Throws()
    {
        string input = "<tag !attr='val'>text</tag>";
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
            features.Dispose();
        });
        StringAssert.Contains("Invalid character '!' found within tag 'tag'", ex.Message);
        Assert.IsInstanceOf<InvalidCharacterInTagException>(ex.InnerException);
    }

    [Test]
    public void Parse_TextBeforeAndAfterTag()
    {
        string input = "before<a>middle</a>after";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        // PrintResults(input, features);

        Assert.AreEqual(1, features.Elements.Length, "Elements count");
        Assert.AreEqual(0, features.Attributes.Length, "Attributes count");
        Assert.AreEqual(3, features.Contents.Length, "Contents count");

        // Element 0: <a>
        AssertSubstring("a", input, features.Elements[0].TagName);

        // Content 0: "before" (root content)
        Assert.AreEqual(-1, features.Contents[0].OwnerElementIndex, "Content 0 Owner (root)");
        Assert.IsTrue(features.Contents[0].IsText, "Content 0 IsText");
        AssertSubstring("before", input, features.Contents[0].Content, "Content 0 Text");

        // Content 1: "middle" (child of <a>)
        Assert.AreEqual(0, features.Contents[1].OwnerElementIndex, "Content 1 Owner (a)");
        Assert.IsTrue(features.Contents[1].IsText, "Content 1 IsText");
        AssertSubstring("middle", input, features.Contents[1].Content, "Content 1 Text");

        // Content 2: "after" (root content)
        Assert.AreEqual(-1, features.Contents[2].OwnerElementIndex, "Content 2 Owner (root)");
        Assert.IsTrue(features.Contents[2].IsText, "Content 2 IsText");
        AssertSubstring("after", input, features.Contents[2].Content, "Content 2 Text");

        features.Dispose();
    }

    [Test]
    public void Parse_OnlyText_CorrectContent()
    {
        string input = "just plain text";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var features);
        // PrintResults(input, features);

        Assert.AreEqual(0, features.Elements.Length, "Elements count");
        Assert.AreEqual(0, features.Attributes.Length, "Attributes count");
        Assert.AreEqual(1, features.Contents.Length, "Contents count");

        // Content 0: "just plain text" (root content)
        Assert.AreEqual(-1, features.Contents[0].OwnerElementIndex, "Content 0 Owner (root)");
        Assert.IsTrue(features.Contents[0].IsText, "Content 0 IsText");
        AssertSubstring("just plain text", input, features.Contents[0].Content, "Content 0 Text");

        features.Dispose();
    }

    [Test]
    public void Parse_Nested()
    {
        string input = "this is <color value=\"#00FF00\">green</color> but this is <color value=\"#FF0000\">red</color> <color value=\"#0000FF\">blue line but we have <color value=\"#00FF00\">green</color> on it</color>";
        MarkupParser.ParseMarkup(input, Allocator.TempJob, out var result);
        Assert.AreEqual(4, result.Elements.Length);
        Assert.AreEqual(4, result.Attributes.Length);
        PrintResults(input, result);
    }
}
