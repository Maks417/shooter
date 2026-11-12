#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>A schema validator must accept the document before it can enter the generic compiler.</summary>
    public sealed class ValidatedRecipe
    {
        public RecipeDocument Document { get; }
        public ContentId SourceHash { get; }
        private readonly byte[] source;
        public byte[] CanonicalSource => (byte[])source.Clone();
        public ValidatedRecipe(RecipeDocument document, ReadOnlySpan<byte> wholeSource, IRecipeValidator validator)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            var validation = validator.Validate(document);
            if (!validation.Valid) throw new FormatException("Recipe schema validation failed: " + string.Join("; ", validation.Issues.Select(i => i.Path + ": " + i.Message)));
            source = CanonicalJson.Encode(wholeSource);
            string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            string reconstructed = "{\"schemaId\":" + Q(document.SchemaId) + ",\"recipeId\":" + Q(document.RecipeId.ToString()) +
                ",\"generatorId\":" + Q(document.GeneratorId) + ",\"generatorVersion\":" + Q(document.GeneratorVersion) + ",\"seed\":" + Q(document.Seed) +
                ",\"parameters\":" + Encoding.UTF8.GetString(document.Parameters) + ",\"dependencies\":[" + string.Join(",", document.Dependencies.Select(id => Q(id.ToString()))) + "]}";
            if (!source.AsSpan().SequenceEqual(CanonicalJson.Encode(Encoding.UTF8.GetBytes(reconstructed)))) throw new FormatException("Source document and validated parameters/identity differ.");
            SourceHash = ContentIdentity.SourceHash(document.SchemaId, source);
        }
        public static ValidatedRecipe FromBox(ReadOnlySpan<byte> source)
        {
            var parsed = BoxRecipe.Parse(source);
            return new ValidatedRecipe(parsed.Document, parsed.CanonicalSource, new BoxRecipeValidator());
        }
    }
}
