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
using System.Threading;

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
            MonthString = cmbBoxMonths.SelectedItem.ToString().Split(new char[] { ':' }).Last().Trim(new char[] { ' ' });
            ParseLastFile();
        }

        //Years
        public ObservableCollection<string> Years { get; set; }

        public string YearString { get; set; }
        private void CmbBxYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            YearString = cmbBxYear.SelectedItem.ToString().Split(new char[] { ':' }).Last().Trim(new char[] { ' ' });
            ParseLastFile();
        }

        private string[] lastFileLines = new string[]{""};

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
                if (lines.Count() > 0)
                    lastFileLines = lines;

                ParseLastFile();
            }
            catch(Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error while opening file: " + ex.ToString());
            }
        }

        private void ParseLastFile()
        {
            try { 
                List<Payer> payers = new List<Payer>();

                MissedMessages.Clear();  //clear missed list for new read

                DateTime latestDateTime = new DateTime(1, 1, 1);
                Payer latestPayer = null;

                foreach (string line in lastFileLines)
                {
                    try
                    {
                        string msgString = "";
                        if (CopiedFromWaWeb)
                        {
                            ////new parse with copy from whatsapp web
                            //handle lines of format: "[16:57, 8.1.2020] XYZ: Shampoo 2,50€"

                            //1. get latest date
                            if (line.StartsWith("["))
                            {
                                try
                                {
                                    string dateTimeString = line.Split(']').First().Trim('[');
                                    DateTime newDateTime = DateTime.ParseExact(dateTimeString, "HH:mm, d.M.yyyy", null);
                                    latestDateTime = newDateTime;
                                }
                                catch (Exception ex) { }
                            }

                            // --> if date doesnt fit into filter continue
                            string messageMonthString = latestDateTime.Month.ToString();
                            if (messageMonthString.Count() < 2)
                                messageMonthString = '0' + messageMonthString;
                            if (messageMonthString != MonthString)
                                continue;
                            if (latestDateTime.Year.ToString() != YearString)
                                continue;

                            //2. get latest payer
                            if (line.StartsWith("["))
                            {
                                string payerName = line.Split(':')[1].Split(']').Last().Trim(' ');
                                if (payers.Where(x => x.Name == payerName).Count() == 0)    //if payer not exists add payer
                                    payers.Add(new Payer(payerName));

                                //add payment to payer
                                latestPayer = payers.Where(x => x.Name == payerName).First();
                            }

                            //3. get payment
                            if (!line.Contains("€"))
                            {
                                MissedMessages.Add(line);
                                continue;
                            }

                            msgString = line.Split(':').Last();
                        }
                        else
                        {

                            //old parse with export function that is now disabled in germany
                            //handles lines of format: "27.08.19, 19:43 - XYZ: Restaurant 22€"

                            //filter month
                            if (line.Split(new char[] { '.' })[1] != MonthString)
                                continue;

                            //filter year
                            if (!YearString.EndsWith(line.Split(new char[] { '.' })[2].Substring(0, 2)))
                            {
                                string[] test = line.Split(new char[] { '.' });
                                continue;
                            }

                            try
                            {
                                string dateTimeString = line.Substring(0, 15);
                                DateTime newDateTime = DateTime.ParseExact(dateTimeString, "dd.MM.yy, HH:mm", null);
                                latestDateTime = newDateTime;
                            }
                            catch (Exception ex) { }

                            //filter contains payment
                            if (!line.Contains("€"))
                            {
                                MissedMessages.Add(line);
                                continue;
                            }

                            //get payer
                            string payerName = null;
                            string msgAndSender = line.Substring(18);
                            payerName = msgAndSender.Split(new char[] { ':' })[0];

                            //get amount and subject
                            msgString = msgAndSender.Split(new char[] { ':' })[1];

                            if (payers.Where(x => x.Name == payerName).Count() == 0)    //if payer not exists add payer
                                payers.Add(new Payer(payerName));

                            //add payment to payer
                            latestPayer = payers.Where(x => x.Name == payerName).First();
                        }

                        float amount = 0;
                        string subject = "";
                        string amountString = null;
                        foreach (string msgPart in msgString.Split(new char[] { ' ' }))
                        {
                            if (!msgPart.Contains("€"))
                            {
                                subject += msgPart + " ";
                                continue;
                            }

                            amountString = msgPart.Trim(new char[] { ' ', '€' });
                        }
                        if (amountString != null && Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator == "." && amountString.Contains(","))   //replace comma with . if english style decimal separators
                        {
                            amountString = amountString.Replace(',', '.');
                        }
                        subject = subject.Trim(new char[] { ' ' });
                        if (!string.IsNullOrEmpty(amountString) && !float.TryParse(amountString, out amount))
                            MissedMessages.Add(line);

                        //add payment to payer
                        if (latestPayer != null)
                            latestPayer.AddPayment(amount, subject, latestDateTime.ToString());
                        else
                            MissedMessages.Add(line);
                    }
                    catch (Exception)
                    {
                        System.Windows.Forms.MessageBox.Show("Error on line: " + line);
                        MissedMessages.Add(line);
                        PropChanged(nameof(MissedMessages));
                    }

                }

                if (payers.Count > 0)
                {
                    Payers.Clear();
                    Sum = 0;
                }

                txtBlckOutput.Text = "";
                foreach (Payer payer in payers)
                {
                    Sum += payer.SumPayed;
                }
                float sumPerPayer = Sum / payers.Count;
                foreach (Payer payer in payers)
                {
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

        private BindingList<string> missedMessages = new BindingList<string>();
        public BindingList<string> MissedMessages
        {
            get => missedMessages;
            set
            {
                missedMessages = value;
                PropChanged(nameof(MissedMessages));
            }
        }

        private bool copiedFromWaWeb = true;
        public bool CopiedFromWaWeb { get => copiedFromWaWeb; set { copiedFromWaWeb = value; PropChanged(nameof(CopiedFromWaWeb)); } }

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
