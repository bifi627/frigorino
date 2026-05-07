using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Frigorino.Web.OpenApi
{
    /// <summary>
    /// Marks non-nullable CLR properties as required in the OpenAPI schema. By default,
    /// Microsoft.AspNetCore.OpenApi treats every property as optional regardless of C# nullability,
    /// which makes generated TS clients see `id?: number` for fields that are always present.
    /// This transformer adds non-nullable value types and non-nullable reference types (via NRT)
    /// to schema.Required so codegen produces non-optional fields.
    /// </summary>
    public sealed class RequiredSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(
            OpenApiSchema schema,
            OpenApiSchemaTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (schema.Properties is null || schema.Properties.Count == 0)
            {
                return Task.CompletedTask;
            }

            var typeInfo = context.JsonTypeInfo;
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return Task.CompletedTask;
            }

            schema.Required ??= new HashSet<string>();

            var nullabilityContext = new NullabilityInfoContext();
            foreach (var propInfo in typeInfo.Properties)
            {
                if (!schema.Properties.ContainsKey(propInfo.Name))
                {
                    continue;
                }

                if (IsNonNullable(propInfo, nullabilityContext))
                {
                    schema.Required.Add(propInfo.Name);
                }
            }

            return Task.CompletedTask;
        }

        private static bool IsNonNullable(JsonPropertyInfo propInfo, NullabilityInfoContext nullabilityContext)
        {
            var type = propInfo.PropertyType;

            if (type.IsValueType)
            {
                return Nullable.GetUnderlyingType(type) is null;
            }

            var nullability = propInfo.AttributeProvider switch
            {
                PropertyInfo pi => nullabilityContext.Create(pi),
                FieldInfo fi => nullabilityContext.Create(fi),
                _ => null
            };

            return nullability?.ReadState == NullabilityState.NotNull;
        }
    }
}
