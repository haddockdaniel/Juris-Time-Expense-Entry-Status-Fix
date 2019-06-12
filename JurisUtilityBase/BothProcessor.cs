using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace JurisUtilityBase
{
    public class BothProcessor
    {

        public BothProcessor(JurisUtility _ju)
        {
            _jurisUtility = _ju;
        }

        JurisUtility _jurisUtility;

        DataSet ds;

        public List<TimeEntry> allTimes = new List<TimeEntry>();

        public List<TimeEntry> correctedTimes = new List<TimeEntry>();

        public List<ExpenseEntry> allExp = new List<ExpenseEntry>();

        public List<ExpenseEntry> correctedExpenses = new List<ExpenseEntry>();

        public string message = "";

        public void processTimeEntries(string ID)
        {

            allTimes.Clear();
            try
            {
                if (ds != null)
                    ds.Clear();
                //go through each status individually

                //due to the size and nature of the data, we will do them by EntryType (6, 7, 8, 9). We ignore all draft time (0-5)
                String SQL = "SELECT  t.EntryID, dbo.jfn_FormatClientCode(CliCode) as clicode,dbo.jfn_FormatMatterCode(MatCode) as matcode " +
                             " ,EntryDate ,e.empname as empName ,BillableFlag ,ActualHoursWork ,Amount, EntryStatus, " +
                             " tbd.tbdid, ut.utid, pb.PBFUTBatch, pb.PBFUTRecNbr, bt.BTID " +
                             " FROM TimeEntry t " +
                             " inner join matter m on m.matsysnbr = t.MatterSysNbr " +
                             " inner join client c on c.clisysnbr = ClientSysNbr " +
                             " inner join employee e on e.empsysnbr = t.TimekeeperSysNbr " +
                             " left outer join timeentrylink tel on tel.entryid = t.entryid " +
                             " left outer join timebatchdetail tbd on tel.tbdid = tbd.tbdid " +
                             " left outer join unbilledtime ut on ut.utid = tbd.tbdid " +
                             " left outer join PreBillFeeItem pb on pb.PBFUTBatch = ut.utbatch and pb.PBFUTRecNbr = ut.UTRecNbr " +
                             " left outer join billedtime bt on bt.btid = tbd.tbdid " +
                             " where t.entrystatus = " + ID;

                ds = _jurisUtility.RecordsetFromSQL(SQL);
                foreach (DataRow dr in ds.Tables[0].Rows)
                {
                    TimeEntry te = new TimeEntry();
                    te.amount = Convert.ToDecimal(dr["Amount"].ToString().Trim());
                    te.hours = Convert.ToDecimal(dr["ActualHoursWork"].ToString().Trim());
                    te.ClientNo = dr["clicode"].ToString().Trim();
                    te.MatterNo = dr["matcode"].ToString().Trim();
                    te.ID = Convert.ToInt32(dr["EntryID"].ToString().Trim());
                    te.Date = DateTime.Parse(dr["EntryDate"].ToString().Trim()).ToString("MM/dd/yyyy");
                    te.oldEntryStatus = Convert.ToInt32(dr["EntryStatus"].ToString().Trim());
                    if (!string.IsNullOrEmpty(dr["tbdid"].ToString().Trim()))
                        te.tbdid = Convert.ToInt32(dr["tbdid"].ToString().Trim());
                    if (!string.IsNullOrEmpty(dr["utid"].ToString().Trim()))
                        te.utid = Convert.ToInt32(dr["utid"].ToString().Trim());
                    if (!string.IsNullOrEmpty(dr["PBFUTBatch"].ToString().Trim()))
                        te.pbbatch = Convert.ToInt32(dr["PBFUTBatch"].ToString().Trim());
                    if (!string.IsNullOrEmpty(dr["PBFUTRecNbr"].ToString().Trim()))
                        te.pbrec = Convert.ToInt32(dr["PBFUTRecNbr"].ToString().Trim());
                    if (!string.IsNullOrEmpty(dr["BTID"].ToString().Trim()))
                        te.btid = Convert.ToInt32(dr["BTID"].ToString().Trim());
                    if (dr["BillableFlag"].ToString().Trim() == "Y")
                        te.isBillable = true;
                    else
                        te.isBillable = false;
                    te.Timekeeper = dr["empName"].ToString().Trim();
                    te.explanation = "";
                    te.ExpCode = "";
                    te.quantity = 1;
                    te.Summarize = false;
                    te.newEntryStatus = -1;
                    allTimes.Add(te);
                }

                ds.Clear();


            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
            }
        }

        public void compareTimeEntries(TimeEntry tt)
        {

            //6 is recorded, 7 is unbilled, 8 is on prebill and 9 is billed
            switch (tt.oldEntryStatus)
            {
                case 6:
                    {

                        //6 means they should be in tdb but NOT in unbilled or billedtime

                        if (tt.tbdid == 0)
                            addToErrorsTime(0, "Recorded time was not in TimeBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it should NOT be in unbilled or billed time
                        {
                            //unbilledtime
                            if (tt.utid != 0)
                            {
                                //see if it is in prebillfeeitem
                                if (tt.pbrec != 0 && tt.pbbatch != 0) //it IS in unbilled AND prebill
                                    addToErrorsTime(8, "Recorded time was on a PreBill. Setting to 'On PreBill'", tt);
                                else //it is NOT in PreBill but IS in unbilledTime
                                    addToErrorsTime(7, "Recorded time was on in UnbilledTime. Setting to Posted", tt);
                            }
                            //billedtime
                            else if (tt.btid != 0) //it IS in billed so it needs to be 9
                                addToErrorsTime(9, "Recorded time was in BilledTime. Setting to Billed", tt);

                        }
                        break;
                    }


                case 7:
                    {

                        //7 means they should be in unbilledtime AND TBD but NOT in prebillfeeitem or billedtime

                        if (tt.tbdid == 0)
                            addToErrorsTime(0, "Unbilled time was not in TimeBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it needs to be in unbilledtime
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is in prebillfeeitem
                                if (tt.pbrec != 0 && tt.pbbatch != 0) //it IS in unbilled AND prebill
                                    addToErrorsTime(8, "Unbilled time was on a PreBill. Setting to 'On PreBill'", tt);
                                // else it is NOT in PreBill so we do nothing because it is IN tbd and IN unbilledtime
                            }
                            else //it is in tbd but NOT unbilledtime so we see if it is in billedtime
                            {
                                //billedtime
                                if (tt.btid != 0) //it IS in billed so it needs to be 9
                                    addToErrorsTime(9, "Recorded time was in BilledTime. Setting to Billed", tt);
                                else //not in billedtime, unbilledtime or prebill so it is a 6
                                    addToErrorsTime(6, "Unbilled time was in TimeBatchDetail but NOT UnbilledTime. Setting to Recorded", tt);

                            }


                        }
                        break;
                    }

                case 8:
                    {

                        //8 means they should be in tdb AND unbilledtime AND prebillfeeitem

                        if (tt.tbdid == 0)
                            addToErrorsTime(0, "Prebill time was not in TimeBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is not in prebillfeeitem
                                if (tt.pbrec == 0 && tt.pbbatch == 0) //it IS NOT in prebill
                                    addToErrorsTime(7, "Prebill time was not on PreBill but WAS in UnbilledTime. Setting to Posted", tt);
                                //else do nothing because it is in all 3 tables
                            }
                            else //NOT in unbilledtime but it IS in tbd
                            {
                                //billedtime
                                if (tt.btid != 0) //it IS in billed so it needs to be 9
                                    addToErrorsTime(9, "Prebill time was in BilledTime. Setting to Billed", tt);
                                else //not in unbilled, billed but IS in tbd
                                    addToErrorsTime(6, "Prebill time was not in UnbilledTime but IS in TimeBatchDetail. Setting to Recorded", tt);
                            }



                        }
                        break;
                    }

                case 9:
                    {
                        //9 means it SHOULD be in TBD and billedtime but NOT in unbilledTime or PreBillFeeItem

                        if (tt.tbdid == 0)
                            addToErrorsTime(0, "Billed time was not in TimeBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it should NOT be in unbilled or billed time
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is in prebillfeeitem
                                if (tt.pbrec != 0 && tt.pbbatch != 0) //it IS in unbilled AND prebill
                                    addToErrorsTime(8, "Billed time was on a PreBill. Setting to 'On PreBill'", tt);
                                else //it is NOT in PreBill
                                    addToErrorsTime(7, "Billed time was in UnbilledTime. Setting to Posted", tt);
                            }
                            //billedtime
                            else if (tt.btid == 0) //it IS NOT in billed so it needs to be 0
                                addToErrorsTime(0, "Billed time was not in BilledTime. Setting to Draft", tt);
                        }
                        break;
                    }
            }//end switch
        }


        private void addToErrorsTime(int entryType, string message, TimeEntry entry)
        {
            entry.explanation = message;
            entry.newEntryStatus = entryType;
            correctedTimes.Add(entry);
        }


        public void updateTimeEntries(List<TimeEntry> tList, int EntryStatus)
        {
            string IDs = "";
            if (tList.Count > 0)
            {
                foreach (TimeEntry tt in tList)
                {
                    IDs = IDs + tt.ID + ",";
                }

                IDs = IDs.TrimEnd(',');

                String SQL = "update timeentry set EntryStatus = " + EntryStatus + " where EntryID in (" + IDs + ")";
                _jurisUtility.ExecuteNonQuery(0, SQL);
            }

        }

        public void processExpenseEntries(string ID)
        {
            if (ds != null)
                ds.Clear();
            //go through each status individually


            //due to the size and nature of the data, we will do them by EntryType (6, 7, 8, 9). We ignore all draft time (0-5)
            string SQL = "SELECT  t.EntryID, dbo.jfn_FormatClientCode(CliCode) as clicode,dbo.jfn_FormatMatterCode(MatCode) as matcode " +
                           "   ,t.EntryDate ,ExpenseScheduleCode ,Units ,Amount, Summarize, EntryStatus " +
                           "   ,tbd.ebdid, ut.ueid, pb.PBEDUEBatch, pb.PBEDUERecNbr, bt.BeID, " +
                            "  pb1.PBESDUEBatch, pb1.PBESDUERecNbr " +
                           "   FROM ExpenseEntry t " +
                           "   inner join matter m on m.matsysnbr = t.MatterSysNbr  " +
                           "   inner join client c on c.clisysnbr = ClientSysNbr  " +
                           "   left outer join ExpenseEntryLink tel on tel.entryid = t.entryid  " +
                            "  left outer join ExpBatchDetail tbd on tel.ebdid = tbd.ebdid  " +
                            "  left outer join UnbilledExpense ut on ut.ueid = tbd.ebdid  " +
                            "  left outer join PreBillExpDetailItem pb on pb.PBEDUEBatch = ut.uebatch and pb.PBEDUERecNbr = ut.UeRecNbr  " +
                            "  left outer join PreBillExpSumDetail pb1 on pb1.PBESDUEBatch = ut.uebatch and pb1.PBESDUERecNbr = ut.UeRecNbr  " +
                           "   left outer join BilledExpenses bt on bt.beid = tbd.ebdid  " +
                           "   where t.entrystatus = " + ID;

            ds = _jurisUtility.RecordsetFromSQL(SQL);

            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                ExpenseEntry te = new ExpenseEntry();
                te.amount = Convert.ToDecimal(dr["Amount"].ToString().Trim());
                te.quantity = Convert.ToDecimal(dr["Units"].ToString().Trim());
                te.ClientNo = dr["clicode"].ToString().Trim();
                te.MatterNo = dr["matcode"].ToString().Trim();
                te.ID = Convert.ToInt32(dr["EntryID"].ToString().Trim());
                te.Date = DateTime.Parse(dr["EntryDate"].ToString().Trim()).ToString("MM/dd/yyyy");
                te.oldEntryStatus = Convert.ToInt32(dr["EntryStatus"].ToString().Trim());
                te.ExpCode = dr["ExpenseScheduleCode"].ToString().Trim();
                if (!string.IsNullOrEmpty(dr["ebdid"].ToString().Trim()))
                    te.tbdid = Convert.ToInt32(dr["ebdid"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["ueid"].ToString().Trim()))
                    te.utid = Convert.ToInt32(dr["ueid"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["PBEDUEBatch"].ToString().Trim()))
                    te.pbbatch = Convert.ToInt32(dr["PBEDUEBatch"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["PBEDUERecNbr"].ToString().Trim()))
                    te.pbrec = Convert.ToInt32(dr["PBEDUERecNbr"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["PBESDUEBatch"].ToString().Trim()))
                    te.pbbatch1 = Convert.ToInt32(dr["PBESDUEBatch"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["PBESDUERecNbr"].ToString().Trim()))
                    te.pbrec1 = Convert.ToInt32(dr["PBESDUERecNbr"].ToString().Trim());
                if (!string.IsNullOrEmpty(dr["BeID"].ToString().Trim()))
                    te.btid = Convert.ToInt32(dr["BeID"].ToString().Trim());
                if (dr["Summarize"].ToString().Trim() == "Y")
                    te.isBillable = true;
                else
                    te.isBillable = false;
                te.explanation = "";
                te.Timekeeper = "";
                te.hours = 0;
                te.isBillable = true;
                te.newEntryStatus = -1;
                allExp.Add(te);
            }

            ds.Clear();


        }


        public void compareExpenseEntries(ExpenseEntry tt)
        {
            switch (tt.oldEntryStatus)
            {
                case 6:
                    {

                        //6 means they should be in tdb but NOT in unbilled or billedtime

                        if (tt.tbdid == 0)
                            addToErrorsExp(0, "Recorded expense was not in ExpBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it should NOT be in unbilled or billed time
                        {
                            //unbilledtime
                            if (tt.utid != 0)
                            {
                                //see if it is in prebillfeeitem
                                if ((tt.pbrec != 0 && tt.pbbatch != 0) || (tt.pbrec1 != 0 && tt.pbbatch1 != 0)) //it IS in unbilled AND prebill
                                    addToErrorsExp(8, "Recorded expense was on a PreBill. Setting to 'On PreBill'", tt);
                                else //it is NOT in PreBill but IS in unbilledTime
                                    addToErrorsExp(7, "Recorded expense was on in UnbilledExp. Setting to Posted", tt);
                            }
                            //billedtime
                            else if (tt.btid != 0) //it IS in billed so it needs to be 9
                                addToErrorsExp(9, "Recorded expense was in BilledExp. Setting to Billed", tt);

                        }
                        break;
                    }


                case 7:
                    {

                        //7 means they should be in unbilledtime AND TBD but NOT in prebillfeeitem or billedtime

                        if (tt.tbdid == 0)
                            addToErrorsExp(0, "Unbilled expense was not in ExpBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it needs to be in unbilledtime
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is in prebillfeeitem
                                if ((tt.pbrec != 0 && tt.pbbatch != 0) || (tt.pbrec1 != 0 && tt.pbbatch1 != 0)) //it IS in unbilled AND prebill
                                    addToErrorsExp(8, "Unbilled expense was on a PreBill. Setting to 'On PreBill'", tt);
                                // else it is NOT in PreBill so we do nothing because it is IN tbd and IN unbilledtime
                            }
                            else //it is in tbd but NOT unbilledtime so we see if it is in billedtime
                            {
                                //billedtime
                                if (tt.btid != 0) //it IS in billed so it needs to be 9
                                    addToErrorsExp(9, "Recorded expense was in BilledExp. Setting to Billed", tt);
                                else //not in billedtime, unbilledtime or prebill so it is a 6
                                    addToErrorsExp(6, "Unbilled expense was in ExpBatchDetail but NOT UnbilledExp. Setting to Recorded", tt);

                            }


                        }
                        break;
                    }

                case 8:
                    {

                        //8 means they should be in tdb AND unbilledtime AND prebillfeeitem

                        if (tt.tbdid == 0)
                            addToErrorsExp(0, "Prebill expense was not in ExpBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is not in prebillfeeitem
                                if ((tt.pbrec != 0 && tt.pbbatch != 0) || (tt.pbrec1 != 0 && tt.pbbatch1 != 0)) //it IS NOT in prebill
                                    addToErrorsExp(7, "Prebill expense was not on PreBill but WAS in UnbilledExp. Setting to Posted", tt);
                                //else do nothing because it is in all 3 tables
                            }
                            else //NOT in unbilledtime but it IS in tbd
                            {
                                //billedtime
                                if (tt.btid != 0) //it IS in billed so it needs to be 9
                                    addToErrorsExp(9, "Prebill texpense was in BilledExp. Setting to Billed", tt);
                                else //not in unbilled, billed but IS in tbd
                                    addToErrorsExp(6, "Prebill expense was not in UnbilledExp but IS in ExpBatchDetail. Setting to Recorded", tt);
                            }



                        }
                        break;
                    }

                case 9:
                    {
                        //9 means it SHOULD be in TBD and billedtime but NOT in unbilledTime or PreBillFeeItem

                        if (tt.tbdid == 0)
                            addToErrorsExp(0, "Billed expense was not in ExpBatchDetail. Setting to Draft", tt);
                        else //it IS in tbd but it should NOT be in unbilled or billed time
                        {
                            //unbilledtime
                            if (tt.utid != 0)//if it IS
                            {
                                //see if it is in prebillfeeitem
                                if (tt.pbrec != 0 && tt.pbbatch != 0) //it IS in unbilled AND prebill
                                    addToErrorsExp(8, "Billed expense was on a PreBill. Setting to 'On PreBill'", tt);
                                else //it is NOT in PreBill
                                    addToErrorsExp(7, "Billed expense was in UnbilledExp. Setting to Posted", tt);
                            }
                            //billedtime
                            else if (tt.btid == 0) //it IS NOT in billed so it needs to be 0
                                addToErrorsExp(0, "Billed expense was not in BilledExp. Setting to Draft", tt);
                        }
                        break;
                    }
            }//end switch




        }
 

        private void addToErrorsExp(int entryType, string message, ExpenseEntry entry)
        {
            entry.explanation = message;
            entry.newEntryStatus = entryType;
            correctedExpenses.Add(entry);
        }

        public void updateExpenseEntries(List<ExpenseEntry> tList, int EntryStatus)
        {
            string IDs = "";
            if (tList.Count > 0)
            {
                foreach (ExpenseEntry tt in tList)
                {
                    IDs = IDs + tt.ID + ",";
                }

                IDs = IDs.TrimEnd(',');

                String SQL = "update ExpenseEntry set EntryStatus = " + EntryStatus + " where EntryID in (" + IDs + ")";
                _jurisUtility.ExecuteNonQuery(0, SQL);
            }

        }










    }
}
