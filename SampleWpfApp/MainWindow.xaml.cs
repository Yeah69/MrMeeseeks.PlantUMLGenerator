namespace MrMeeseeks.ResXToViewModelGenerator.SampleWpfApp
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        public string Both { get; set; } = "";

        public string Get { get; } = "";

        public string Set { private get; set; } = "";
    }
}