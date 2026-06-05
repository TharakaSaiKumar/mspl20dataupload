using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class FlowService : IFlowService
{
    public void Publish(FlowContext context, string key, string? value)
        => context.Publish(key, value);

    public string? Consume(FlowContext context, string key)
        => context.Consume(key);

    public bool Exists(FlowContext context, string key)
        => context.Contains(key);

    public void Clear(FlowContext context)
        => context.Clear();
}
