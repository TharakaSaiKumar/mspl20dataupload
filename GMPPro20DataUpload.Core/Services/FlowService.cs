using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class FlowService : IFlowService
{
    public void Publish(FlowContext context, string key, string? value)
        => throw new NotImplementedException();

    public string? Consume(FlowContext context, string key)
        => throw new NotImplementedException();
}
