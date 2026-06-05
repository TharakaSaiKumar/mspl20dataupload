using GMPPro20DataUpload.Core.Interfaces;

namespace GMPPro20DataUpload.UI;

public partial class Form1 : Form
{
    private readonly IProcessingService _processingService;
    private readonly IValidationService _validationService;

    public Form1(IProcessingService processingService, IValidationService validationService)
    {
        _processingService = processingService;
        _validationService = validationService;
        InitializeComponent();
    }
}
