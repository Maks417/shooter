#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace Shooter.Contracts
{
    public sealed class Diagnostic
    {
        public ErrorCode Code { get; }
        public string Path { get; }
        public string Message { get; }
        public Diagnostic(ErrorCode code, string path, string message)
        { Require.Enum(code, nameof(code)); Code = code; Path = Require.Text(path, nameof(path), 512); Message = Require.Text(message, nameof(message), 512); }
    }
    public sealed class ValidationReport
    {
        public bool Valid { get; }
        public FrozenList<Diagnostic> Issues { get; }
        public ValidationReport(bool valid, IReadOnlyList<Diagnostic> issues)
        {
            Issues = new FrozenList<Diagnostic>(issues, 128);
            foreach (var issue in Issues) Require.That(issue != null, nameof(issues));
            Require.That(valid ? Issues.Count == 0 : Issues.Count > 0, nameof(valid)); Valid = valid;
        }
    }
    /// <summary>Schema/canonical-byte validation is the IRecipeValidator's responsibility before generation.</summary>
    public sealed class RecipeDocument
    {
        public DefinitionId RecipeId { get; }
        public string SchemaId { get; }
        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public string Seed { get; }
        public ulong SeedValue { get; }
        private readonly byte[] parameters;
        public ReadOnlySpan<byte> Parameters => parameters;
        public FrozenList<ContentId> Dependencies { get; }
        public RecipeDocument(DefinitionId recipeId, string schemaId, string generatorId, string generatorVersion, string seed, ReadOnlySpan<byte> parameters, IReadOnlyList<ContentId> dependencies)
        {
            Require.That(recipeId.IsValid && parameters.Length > 0 && parameters.Length <= 1024 * 1024, "recipe");
            Require.That(ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong numericSeed) && numericSeed.ToString(CultureInfo.InvariantCulture) == seed, nameof(seed));
            Dependencies = new FrozenList<ContentId>(dependencies, 256);
            for (int i = 1; i < Dependencies.Count; i++) Require.That(Dependencies[i - 1].CompareTo(Dependencies[i]) < 0, "sorted unique dependencies");
            RecipeId = recipeId; SchemaId = Require.Text(schemaId, nameof(schemaId)); GeneratorId = Require.Text(generatorId, nameof(generatorId));
            GeneratorVersion = Require.Text(generatorVersion, nameof(generatorVersion)); Seed = seed; SeedValue = numericSeed; this.parameters = parameters.ToArray();
        }
    }
    public sealed class GenerationRequest
    {
        public RecipeDocument Recipe { get; }
        public string OutputPlatform { get; }
        public string VisualProfile { get; }
        public ContentId ToolchainHash { get; }
        public GenerationRequest(RecipeDocument recipe, string outputPlatform, string visualProfile, ContentId toolchainHash)
        { Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe)); OutputPlatform = Require.Text(outputPlatform, nameof(outputPlatform)); VisualProfile = Require.Text(visualProfile, nameof(visualProfile)); ToolchainHash = toolchainHash; }
    }
    public sealed class GeneratedArtifact
    {
        public ContentKind Kind { get; }
        public string Variant { get; }
        public uint FormatVersion { get; }
        private readonly byte[] payload;
        public ReadOnlySpan<byte> Payload => payload;
        public ContentId Sha256 { get; }
        public GeneratedArtifact(ContentKind kind, string variant, uint formatVersion, ReadOnlySpan<byte> payload, ContentId sha256)
        {
            Require.Enum(kind, nameof(kind)); Require.That(formatVersion > 0 && payload.Length <= ContractVersion.MaxArtifactBytes, "artifact limits");
            this.payload = payload.ToArray();
            using var hash = SHA256.Create();
            Require.That(new ContentId(hash.ComputeHash(this.payload)) == sha256, "artifact hash");
            Kind = kind; Variant = Require.Text(variant, nameof(variant)); FormatVersion = formatVersion; Sha256 = sha256;
        }
    }
    public sealed class GenerationResult
    {
        public ErrorCode Error { get; }
        public FrozenList<GeneratedArtifact> Artifacts { get; }
        public Aabb Bounds { get; }
        public FrozenList<Diagnostic> Diagnostics { get; }
        public GenerationResult(ErrorCode error, IReadOnlyList<GeneratedArtifact> artifacts, Aabb bounds, IReadOnlyList<Diagnostic> diagnostics)
        {
            Require.Enum(error, nameof(error)); Artifacts = new FrozenList<GeneratedArtifact>(artifacts, 256); Diagnostics = new FrozenList<Diagnostic>(diagnostics, 128);
            long total = 0;
            foreach (var artifact in Artifacts) { if (artifact == null) throw new ArgumentException("Null artifact", nameof(artifacts)); total += artifact.Payload.Length; }
            foreach (var diagnostic in Diagnostics) Require.That(diagnostic != null, nameof(diagnostics));
            Require.That(total <= 256L * 1024 * 1024, "content total");
            Require.That(error == ErrorCode.None ? Artifacts.Count > 0 : Artifacts.Count == 0, "generation result");
            Error = error; Bounds = bounds;
        }
    }
}
