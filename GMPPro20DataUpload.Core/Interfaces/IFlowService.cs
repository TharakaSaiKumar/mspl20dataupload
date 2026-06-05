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
}
