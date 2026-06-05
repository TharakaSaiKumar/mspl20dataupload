using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IFlowService
{
    /// <summary>
    /// Stores a value in the FlowContext under the given flow key (publish action).
    /// </summary>
    void Publish(FlowContext context, string key, string? value);

    /// <summary>
    /// Retrieves a value from the FlowContext by flow key (consume action).
    /// Returns null if the key has not been published yet.
    /// </summary>
    string? Consume(FlowContext context, string key);

    /// <summary>
    /// Returns true if the given flow key has been published in the context.
    /// Use before Consume to guard against missing keys.
    /// </summary>
    bool Exists(FlowContext context, string key);

    /// <summary>
    /// Clears all published values from the FlowContext.
    /// ProcessingService decides when to call this.
    /// </summary>
    void Clear(FlowContext context);
}
