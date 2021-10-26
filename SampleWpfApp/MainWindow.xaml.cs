namespace MrMeeseeks.PlantUMLGenerator.SampleWpfApp
{
    public interface IB : IA {}
    public interface IA {}
    public class A : IA {}
    
    public class B : A, IB {}

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