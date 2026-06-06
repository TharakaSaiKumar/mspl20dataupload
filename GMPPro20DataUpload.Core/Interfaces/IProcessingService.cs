using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IProcessingService
{
    /// <summary>
    /// Orchestrates a complete processing run:
    /// schema load → template load → per-row processing →
    /// flow → cache → MongoDB → output.
    ///
    /// Progress strings follow the format "Processing row X of Y"
    /// and are reported via IProgress after each row completes.
    ///
    /// When cancellationToken is cancelled, the current row finishes
    /// processing before the run stops (no abrupt termination).
    ///
    /// Returns a fully populated ProcessingContext including all
    /// per-row ProcessResult entries.
    /// </summary>
    Task<ProcessingContext> ProcessAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        string outputPath,
        MongoConfiguration mongoConfig,
        IProgress<string> progress,
        CancellationToken cancellationToken);
}
