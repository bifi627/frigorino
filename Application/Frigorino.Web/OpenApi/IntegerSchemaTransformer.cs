using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Frigorino.Web.OpenApi
{
    /// <summary>
    /// Microsoft.AspNetCore.OpenApi emits integer properties as `type: [integer, string]`
    /// with a numeric regex `pattern` to support stringified-number JSON inputs. This makes
    /// generated TypeScript clients see `number | string` (or `any` in OpenAPI 3.0 where the
    /// multi-type union isn't legal). This transformer collapses such schemas back to a plain
    /// integer so codegen produces `number`. See https://github.com/dotnet/aspnetcore/issues/61038.
    /// </summary>
    public sealed class IntegerSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(
            OpenApiSchema schema,
            OpenApiSchemaTransformerContext context,
            CancellationToken cancellationToken)
        {
            var type = context.JsonTypeInfo.Type;
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(int) || actualType == typeof(long)
                || actualType == typeof(short) || actualType == typeof(byte)
                || actualType == typeof(uint) || actualType == typeof(ulong)
                || actualType == typeof(ushort) || actualType == typeof(sbyte))
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Pattern = null;
            }

            return Task.CompletedTask;
        }
    }
}
