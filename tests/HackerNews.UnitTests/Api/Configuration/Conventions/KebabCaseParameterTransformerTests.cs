using HackerNews.Api.Configuration.Conventions;

namespace HackerNews.UnitTests.Api.Configuration.Conventions;

[TestFixture]
public class KebabCaseParameterTransformerTests
{
    private KebabCaseParameterTransformer _transformer;

    [SetUp]
    public void SetUp()
    {
        _transformer = new KebabCaseParameterTransformer();
    }

    [Test]
    public void TransformOutbound_NullInput_ReturnsNull()
    {
        Assert.That(_transformer.TransformOutbound(null), Is.Null);
    }

    [TestCase("Stories", "stories")]
    [TestCase("BestStories", "best-stories")]
    [TestCase("GetBestStories", "get-best-stories")]
    [TestCase("ApiController", "api-controller")]
    public void TransformOutbound_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.That(_transformer.TransformOutbound(input), Is.EqualTo(expected));
    }
}
