using Frigorino.Infrastructure.Services;
using Frigorino.Infrastructure.Tasks;
using Xunit;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeAttachmentBlobReferencesTests
    {
        [Fact]
        public void AreaName_MatchesConstant()
        {
            var source = new RecipeAttachmentBlobReferences(null!);
            Assert.Equal(BlobAreas.RecipeAttachment, source.AreaName);
            Assert.Equal("recipe-attachment", source.AreaName);
        }
    }
}
