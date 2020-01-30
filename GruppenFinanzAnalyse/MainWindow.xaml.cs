using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace GruppenFinanzAnalyse
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            Month = DateTime.Now.Month > 1 ? DateTime.Now.Month - 1 : 12;

            Years = new ObservableCollection<string>();
            for (int i = 0; i < 10; i++)
                Years.Add((DateTime.Now.Year - i).ToString());  //fill years combo box

            InitializeComponent();

            this.DataContext = this;

            txtBlckOutput.Text = "Export des Gruppenverlaufs per Mail senden und per Drag&Drop hier rein ziehen.";

            cmbBxYear.SelectedIndex = Month == 1 ? 1 : 0;  //select last (current year) or second to last (last year) if month is January 
        }

        private void PropChanged(string propName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName)); }
        public event PropertyChangedEventHandler PropertyChanged;

        //Months
        public int Month { get; set; } = 0;
        public string MonthString { get; set; } = "01";
        private void Months_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MonthString = cmbBoxMonths.SelectedItem.ToString().Split(new char[] { ':' }).Last().Trim(new char[] { ' '});
        }

        //Years
        public ObservableCollection<string> Years { get; set; }

        public string YearString { get; set; }
        private void CmbBxYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            YearString = cmbBxYear.SelectedItem.ToString().Split(new char[] { ':' }).Last().Trim(new char[] { ' ' });
        }

        private void Drop_Handler(object sender, DragEventArgs e)
        {
            try
            {

                string[] droppedFiles = null;

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    droppedFiles = e.Data.GetData(DataFormats.FileDrop, true) as string[];

                if (droppedFiles == null || droppedFiles.Count() < 1)
                {
                    System.Windows.Forms.MessageBox.Show("Ungültiger drop...", "Falscho", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                if (droppedFiles.Count() > 1)
                {
                    System.Windows.Forms.MessageBox.Show("Maximal eine Datei bitte...", "Falscho", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                string path = droppedFiles[0];

                string[] lines = File.ReadAllLines(path);

                List<Payer> payers = new List<Payer>();

                foreach (string line in lines)
                {
                    try
                    {
                        //filter contains payment
                        if (!line.Contains("€"))
                            continue;

                        //filter month
                        if (line.Split(new char[] { '.' })[1] != MonthString)
                            continue;

                        //filter year
                        if (!YearString.EndsWith(line.Split(new char[] { '.' })[2].Substring(0,2)))
                        {
                            string[] test = line.Split(new char[] { '.' });
                            continue;
                        }

                        //get payer
                        string payerName = null;
                        string msgAndSender = line.Substring(18);
                        payerName = msgAndSender.Split(new char[] { ':' })[0];

                        //get amount and subject
                        float amount = 0;
                        string subject = "";
                        string msg = msgAndSender.Split(new char[] { ':' })[1];
                        string amountString = null;
                        foreach (string msgPart in msg.Split(new char[] { ' ' }))
                        {
                            if (!msgPart.Contains("€"))
                            {
                                subject += msgPart + " ";
                                continue;
                            }

                            amountString = msgPart.Trim(new char[] { ' ', '€' });
                        }
                        subject = subject.Trim(new char[] { ' ' });
                        if (!string.IsNullOrEmpty(amountString) && !float.TryParse(amountString, out amount))
                            System.Windows.Forms.MessageBox.Show("Fehler beim konvertieren von " + amountString + "zu float.");

                        if (payers.Where(x => x.Name == payerName).Count() == 0)    //if payer not exists add payer
                            payers.Add(new Payer(payerName));

                        //add payment to payer
                        payers.Where(x => x.Name == payerName).First().AddPayment(amount, subject, line.Split(',')[0]);
                    }
                    catch (Exception)
                    {
                        System.Windows.Forms.MessageBox.Show("Error on line: " + line);
                    }

                }

                txtBlckOutput.Text = "";
                //Output(" - " + MonthFromNumber(Month) + " " + YearString + " - ");
                //Output(" ");

                foreach (Payer payer in payers)
                {
                    //Output("\r\n" + payer.Name + " hat " + payer.SumPayed + " € bezahlt.");
                    Sum += payer.SumPayed;
                }

                //Output(" ");

                //Output("\r\nInsgesamt wurden " + sum + " € ausgegeben.");

                //Output(" ");

                float sumPerPayer = Sum / payers.Count;

                foreach (Payer payer in payers)
                {
                    //if (payer.SumPayed > sumPerPayer)
                    //    Output(payer.Name + " bekommt " + (payer.SumPayed - sumPerPayer) + " €.");
                    //else
                    //    Output(payer.Name + " bezahlt " + (sumPerPayer - payer.SumPayed) + " €.");

                    payer.CompensationPayment = sumPerPayer - payer.SumPayed;

                    Payers.Add(payer);
                }

            }

            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Fehler beim öffnen der Datei: " + ex.ToString(), "Falscho", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private BindingList<Payer> payers = new BindingList<Payer>();
        public BindingList<Payer> Payers
        {
            get => payers;
            set
            {
                if (payers == value) return;

                payers = value;
                PropChanged(nameof(Payers));
            }
        }

        private float sum = 0;
        public float Sum
        {
            get => sum;
            set
            {
                if (sum == value) return;

                sum = value;
                PropChanged(nameof(Sum));
            }
        }

        private void Output(string output)
        {
            //if (output.StartsWith("\r\n"))
            //    txtBlckOutput.Text += output;
            //else
            //    txtBlckOutput.Text += "\r\n" + output;
        }

        private string MonthFromNumber(int num)
        {
            switch (num)
            {
                case 0: return "Weihnachten";
                case 1: return "Januar";
                case 2: return "Februar";
                case 3: return "März";
                case 4: return "April";
                case 5: return "Mai";
                case 6: return "Juni";
                case 7: return "Juli";
                case 8: return "August";
                case 9: return "September";
                case 10: return "Oktober";
                case 11: return "Novemeber";
                case 12: return "December";
                default: return "Unbekannter Monat";
            }
        }

        public class Payer : INotifyPropertyChanged
        {
            public Payer(string name)
            {
                Name = name;
                Payments = new BindingList<Payment>();
                SumPayed = 0;
            }

            public string Name { get; private set; }
            public float SumPayed { get; private set; }
            public BindingList<Payment> Payments { get; private set; }

            public float CompensationPayment { get; set; }

            public void AddPayment(float amount, string subject, string date)
            {
                SumPayed += amount;
                Payments.Add(new Payment(amount, subject, date));
            }

            private void PropChanged(string propName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName)); }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class Payment : INotifyPropertyChanged
        {
            public Payment(float amount, string subject, string date)
            {
                Subject = subject;
                Amount = amount;
                Date = date;
            }
            public string Subject { get; private set; }
            public float Amount { get; private set; }
            public string Date { get; private set; }

            private void PropChanged(string propName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName)); }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
