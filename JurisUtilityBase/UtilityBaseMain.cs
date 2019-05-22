using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Globalization;
using Gizmox.Controls;
using JDataEngine;
using JurisAuthenticator;
using JurisUtilityBase.Properties;
using System.Data.OleDb;
using System.Reflection;
using System.ComponentModel;

namespace JurisUtilityBase
{
    public partial class UtilityBaseMain : Form
    {
        #region Private  members

        public static JurisUtility _jurisUtility;

        #endregion

        #region Public properties

        public string CompanyCode { get; set; }

        public string JurisDbName { get; set; }

        public string JBillsDbName { get; set; }

        public int FldClient { get; set; }

        public int FldMatter { get; set; }

        int totalRecords = 0;

        List<string> timeEntries = new List<string>();

        List<string> expEntries = new List<string>();

        public TimeProcessor tproc = new TimeProcessor(_jurisUtility);

        public ExpenseProcessor eproc = new ExpenseProcessor(_jurisUtility);

        public BothProcessor bproc = new BothProcessor(_jurisUtility);

        DataSet Errors;

        DataSet Errors1;

        int current = 0;

        #endregion

        #region Constructor

        public UtilityBaseMain()
        {
            InitializeComponent();
            _jurisUtility = new JurisUtility();
        }

        #endregion

        #region Public methods

        public void LoadCompanies()
        {
            var companies = _jurisUtility.Companies.Cast<object>().Cast<Instance>().ToList();
//            listBoxCompanies.SelectedIndexChanged -= listBoxCompanies_SelectedIndexChanged;
            listBoxCompanies.ValueMember = "Code";
            listBoxCompanies.DisplayMember = "Key";
            listBoxCompanies.DataSource = companies;
//            listBoxCompanies.SelectedIndexChanged += listBoxCompanies_SelectedIndexChanged;
            var defaultCompany = companies.FirstOrDefault(c => c.Default == Instance.JurisDefaultCompany.jdcJuris);
            if (companies.Count > 0)
            {
                listBoxCompanies.SelectedItem = defaultCompany ?? companies[0];
            }
        }

        #endregion

        #region MainForm events

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void listBoxCompanies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_jurisUtility.DbOpen)
            {
                _jurisUtility.CloseDatabase();
            }
            CompanyCode = "Company" + listBoxCompanies.SelectedValue;
            _jurisUtility.SetInstance(CompanyCode);
            JurisDbName = _jurisUtility.Company.DatabaseName;
            JBillsDbName = "JBills" + _jurisUtility.Company.Code;
            _jurisUtility.OpenDatabase();
            if (_jurisUtility.DbOpen)
            {
                ///GetFieldLengths();
            }

        }



        #endregion

        #region Private methods

        private void DoDaFix()
        {
            // Enter your SQL code here
            // To run a T-SQL statement with no results, int RecordsAffected = _jurisUtility.ExecuteNonQueryCommand(0, SQL);
            // To get an ADODB.Recordset, ADODB.Recordset myRS = _jurisUtility.RecordsetFromSQL(SQL);


            //get all entryids into a list, count them and then send them one at a time to the time/expense processor
            current = 0;
            Errors = null;
            Errors1 = null;

            if (radioButtonT.Checked)
            {
                getTotalRecords(1);
                TimeProcessor tpThread = new TimeProcessor(_jurisUtility);
                backgroundWorkerTime.RunWorkerAsync(tpThread);
            }
            else if (radioButtonE.Checked)
            {
                getTotalRecords(2);
                ExpenseProcessor epThread = new ExpenseProcessor(_jurisUtility);
                backgroundWorkerExp.RunWorkerAsync(epThread);
            }
            else if (radioButtonTE.Checked)
            {
                getTotalRecords(3);
                BothProcessor bpThread = new BothProcessor(_jurisUtility);
                backgroundWorkerAll.RunWorkerAsync(bpThread);
            }
            else
                MessageBox.Show("Please select an Entry Type before continuing", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        }


        private DialogResult seePreReport()
        {
            DialogResult ddr = new DialogResult();
            ddr = MessageBox.Show("The process has gathered all the needed data." + "\r\n" + "Would you like to see a Pre Report before we make changes?", "Pre Report Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return ddr;
        }


        public static DataTable ConvertTo<T>(IList<T> genericList)
        {
            //create DataTable Structure
            DataTable dataTable = CreateTable<T>();
            Type entType = typeof(T);
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(entType);
            //get the list item and add into the list
            foreach (T item in genericList)
            {
                DataRow row = dataTable.NewRow();
                foreach (PropertyDescriptor prop in properties)
                {
                    row[prop.Name] = prop.GetValue(item);
                }
                dataTable.Rows.Add(row);
            }
            return dataTable;
        }

        public static DataTable CreateTable<T>()
        {
            //T –> ClassName
            Type entType = typeof(T);
            //set the datatable name as class name
            DataTable dataTable = new DataTable(entType.Name);
            //get the property list
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(entType);
            foreach (PropertyDescriptor prop in properties)
            {
                //add property as column
                dataTable.Columns.Add(prop.Name, prop.PropertyType);
            }
            return dataTable;
        }

        private void getTotalRecords(int flag)
        {
            String SQL = "";
            DataSet ff;
            if (flag == 1) //time only
            {
                SQL = "select entryid from timeentry where entrystatus in (6,7,8,9)";
                ff = _jurisUtility.RecordsetFromSQL(SQL);
                foreach (DataRow r in ff.Tables[0].Rows)
                    timeEntries.Add(r[0].ToString());

                totalRecords = timeEntries.Count();
                ff.Clear();
            }
            else if (flag == 2) //exp only
            {
                SQL = "select entryid from expenseentry where entrystatus in (6,7,8,9)";
                ff = _jurisUtility.RecordsetFromSQL(SQL);
                foreach (DataRow r in ff.Tables[0].Rows)
                    expEntries.Add(r[0].ToString());

                totalRecords = expEntries.Count();
                ff.Clear();
            }
            else if (flag == 3) //both
            {
                SQL = "select entryid from timeentry where entrystatus in (6,7,8,9)";
                ff = _jurisUtility.RecordsetFromSQL(SQL);
                foreach (DataRow r in ff.Tables[0].Rows)
                    timeEntries.Add(r[0].ToString());

                totalRecords = timeEntries.Count();
                ff.Clear();
                SQL = "select entryid from expenseentry where entrystatus in (6,7,8,9)";
                ff = _jurisUtility.RecordsetFromSQL(SQL);
                foreach (DataRow r in ff.Tables[0].Rows)
                    expEntries.Add(r[0].ToString());

                totalRecords = totalRecords + expEntries.Count();
                ff.Clear();
            }
        }



        private bool VerifyFirmName()
        {
            //    Dim SQL     As String
            //    Dim rsDB    As ADODB.Recordset
            //
            //    SQL = "SELECT CASE WHEN SpTxtValue LIKE '%firm name%' THEN 'Y' ELSE 'N' END AS Firm FROM SysParam WHERE SpName = 'FirmName'"
            //    Cmd.CommandText = SQL
            //    Set rsDB = Cmd.Execute
            //
            //    If rsDB!Firm = "Y" Then
            return true;
            //    Else
            //        VerifyFirmName = False
            //    End If

        }

        private bool FieldExistsInRS(DataSet ds, string fieldName)
        {

            foreach (DataColumn column in ds.Tables[0].Columns)
            {
                if (column.ColumnName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }


        private static bool IsDate(String date)
        {
            try
            {
                DateTime dt = DateTime.Parse(date);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNumeric(object Expression)
        {
            double retNum;

            bool isNum = Double.TryParse(Convert.ToString(Expression), System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
            return isNum; 
        }

        private void WriteLog(string comment)
        {
            var sql =
                string.Format("Insert Into UtilityLog(ULTimeStamp,ULWkStaUser,ULComment) Values('{0}','{1}', '{2}')",
                    DateTime.Now, GetComputerAndUser(), comment);
            _jurisUtility.ExecuteNonQueryCommand(0, sql);
        }

        private string GetComputerAndUser()
        {
            var computerName = Environment.MachineName;
            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var userName = (windowsIdentity != null) ? windowsIdentity.Name : "Unknown";
            return computerName + "/" + userName;
        }

        /// <summary>
        /// Update status bar (text to display and step number of total completed)
        /// </summary>
        /// <param name="status">status text to display</param>
        /// <param name="step">steps completed</param>
        /// <param name="steps">total steps to be done</param>
        private void UpdateStatus(string status, long step, long steps)
        {
            labelCurrentStatus.Text = status;

            if (steps == 0)
            {
                progressBar.Value = 0;
                labelPercentComplete.Text = string.Empty;
            }
            else
            {
                double pctLong = Math.Round(((double)step/steps)*100.0);
                int percentage = (int)Math.Round(pctLong, 0);
                if ((percentage < 0) || (percentage > 100))
                {
                    progressBar.Value = 0;
                    labelPercentComplete.Text = string.Empty;
                }
                else
                {
                    progressBar.Value = percentage;
                    labelPercentComplete.Text = string.Format("{0} percent complete", percentage);
                }
            }
        }

        private void DeleteLog()
        {
            string AppDir = Path.GetDirectoryName(Application.ExecutablePath);
            string filePathName = Path.Combine(AppDir, "VoucherImportLog.txt");
            if (File.Exists(filePathName + ".ark5"))
            {
                File.Delete(filePathName + ".ark5");
            }
            if (File.Exists(filePathName + ".ark4"))
            {
                File.Copy(filePathName + ".ark4", filePathName + ".ark5");
                File.Delete(filePathName + ".ark4");
            }
            if (File.Exists(filePathName + ".ark3"))
            {
                File.Copy(filePathName + ".ark3", filePathName + ".ark4");
                File.Delete(filePathName + ".ark3");
            }
            if (File.Exists(filePathName + ".ark2"))
            {
                File.Copy(filePathName + ".ark2", filePathName + ".ark3");
                File.Delete(filePathName + ".ark2");
            }
            if (File.Exists(filePathName + ".ark1"))
            {
                File.Copy(filePathName + ".ark1", filePathName + ".ark2");
                File.Delete(filePathName + ".ark1");
            }
            if (File.Exists(filePathName ))
            {
                File.Copy(filePathName, filePathName + ".ark1");
                File.Delete(filePathName);
            }

        }

            

        private void LogFile(string LogLine)
        {
            string AppDir = Path.GetDirectoryName(Application.ExecutablePath);
            string filePathName = Path.Combine(AppDir, "VoucherImportLog.txt");
            using (StreamWriter sw = File.AppendText(filePathName))
            {
                sw.WriteLine(LogLine);
            }	
        }
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            DoDaFix();
        }

        private void buttonReport_Click(object sender, EventArgs e)
        {

            System.Environment.Exit(0);
          
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void backgroundWorkerTime_DoWork(object sender, DoWorkEventArgs e)
        {
            tproc = e.Argument as TimeProcessor;
            BackgroundWorker worker = sender as BackgroundWorker;
            foreach (string ID in timeEntries)
            {
                try
                {
                    tproc.processTimeEntries(ID);
                    current++;
                    //UpdateStatus("Updating....", current, totalRecords);
                    backgroundWorkerTime.ReportProgress(current);

                }
                catch (Exception ex1)
                {
                    e.Result = ex1;
                }
            }

            DataTable dt = ConvertTo(tproc.correctedTimes);
            if (dt == null)
                MessageBox.Show("dt is null");
            Errors = new DataSet();
            Errors.Tables.Add(dt);
            DialogResult tpr = seePreReport();
            if (tpr == DialogResult.Yes)
            {
                ReportDisplay rpds = new ReportDisplay(Errors, null, 0);
                rpds.ShowDialog();
                foreach (TimeEntry tt in tproc.correctedTimes)
                {
                    tproc.updateTimeEntries(tt);
                    current++;
                }
            }
            else if (tpr == DialogResult.No)
            {
                UpdateStatus("", 0, totalRecords);
                foreach (TimeEntry tt in tproc.correctedTimes)
                {
                    tproc.updateTimeEntries(tt);
                    current++;
                }
            }
            else if (tpr == DialogResult.Cancel)
            {
                Environment.ExitCode = 1;
                Application.Exit();
            }
        }

        private void backgroundWorkerTime_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateStatus("Updating....", current, totalRecords * 2);
        }

        private void backgroundWorkerTime_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateStatus("All entries updated.", totalRecords * 2, totalRecords * 2);

            MessageBox.Show("The process is complete", "Confirmation", MessageBoxButtons.OK, MessageBoxIcon.None);
            Environment.ExitCode = 1;
            Application.Exit();
        }

        private void UtilityBaseMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }

        private void backgroundWorkerExp_DoWork(object sender, DoWorkEventArgs e)
        {
            eproc = e.Argument as ExpenseProcessor;
            BackgroundWorker worker = sender as BackgroundWorker;
            foreach (string ID in expEntries)
            {
                try
                {
                    eproc.processExpenseEntries(ID);
                    current++;
                    backgroundWorkerExp.ReportProgress(current);

                }
                catch (Exception ex1)
                {
                    MessageBox.Show("Error: " + ex1.Message);
                }
            }

            DataTable dt = ConvertTo(eproc.correctedExpenses);
            Errors.Tables.Add(dt);
            DialogResult tpr = seePreReport();
            if (tpr == DialogResult.Yes)
            {
                ReportDisplay rpds = new ReportDisplay(null, Errors, 1);
                rpds.ShowDialog();
                foreach (ExpenseEntry ee in eproc.correctedExpenses)
                {
                    eproc.updateExpEntries(ee);
                    current++;
                }
            }
            else if (tpr == DialogResult.No)
            {
                foreach (ExpenseEntry ee in eproc.correctedExpenses)
                {
                    eproc.updateExpEntries(ee);
                    current++;
                }
            }
            else if (tpr == DialogResult.Cancel)
            {
                Environment.ExitCode = 1;
                Application.Exit();
            }
        }

        private void backgroundWorkerExp_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateStatus("Updating....", current, totalRecords * 2);
        }

        private void backgroundWorkerExp_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateStatus("All entries updated.", totalRecords * 2, totalRecords * 2);

            MessageBox.Show("The process is complete", "Confirmation", MessageBoxButtons.OK, MessageBoxIcon.None);
            Environment.ExitCode = 1;
            Application.Exit();
        }


        private void backgroundWorkerAll_DoWork(object sender, DoWorkEventArgs e)
        {
            bproc = e.Argument as BothProcessor;
            BackgroundWorker worker = sender as BackgroundWorker;
            foreach (string ID in expEntries)
            {
                try
                {
                    bproc.processExpenseEntries(ID);
                    current++;
                    backgroundWorkerAll.ReportProgress(current);

                }
                catch (Exception ex1)
                {
                    MessageBox.Show("Error: " + ex1.InnerException);
                }
            }

            foreach (string ID in timeEntries)
            {
                try
                {
                    bproc.processTimeEntries(ID);
                    current++;
                    backgroundWorkerAll.ReportProgress(current);

                }
                catch (Exception ex1)
                {
                    MessageBox.Show("Error: " + ex1.InnerException);
                }
            }


            DataTable dt = ConvertTo(bproc.correctedTimes);
            DataTable dx = ConvertTo(bproc.correctedExpenses);
            Errors.Tables.Add(dt);
            Errors1.Tables.Add(dx);
            DialogResult tpr = seePreReport();
            if (tpr == DialogResult.Yes)
            {
                ReportDisplay rpds = new ReportDisplay(Errors, Errors1, 2);
                rpds.ShowDialog();
                foreach (TimeEntry tt in tproc.correctedTimes)
                {
                    bproc.updateTimeEntries(tt);
                    current++;
                }
                foreach (ExpenseEntry ee in eproc.correctedExpenses)
                {
                    eproc.updateExpEntries(ee);
                    current++;
                }
            }
            else if (tpr == DialogResult.No)
            {
                foreach (TimeEntry tt in tproc.correctedTimes)
                {
                    bproc.updateTimeEntries(tt);
                    current++;
                }
                foreach (ExpenseEntry ee in eproc.correctedExpenses)
                {
                    eproc.updateExpEntries(ee);
                    current++;
                }
            }
            else if (tpr == DialogResult.Cancel)
            {
                Environment.ExitCode = 1;
                Application.Exit();
            }
        }

        private void backgroundWorkerAll_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateStatus("Updating....", current, totalRecords * 2);
        }

        private void backgroundWorkerAll_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateStatus("All entries updated.", totalRecords * 2, totalRecords * 2);

            MessageBox.Show("The process is complete", "Confirmation", MessageBoxButtons.OK, MessageBoxIcon.None);
            Environment.ExitCode = 1;
            Application.Exit();
        }


    }
}
