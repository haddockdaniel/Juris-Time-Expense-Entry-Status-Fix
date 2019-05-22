using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace JurisUtilityBase
{
    public class TimeProcessor
    {

        public TimeProcessor(JurisUtility _ju)
        {
            _jurisUtility = _ju;
        }

        public string message {get;set;}

        JurisUtility _jurisUtility;

        DataSet ds = new DataSet();

        DataSet temp = new DataSet();

        int batchDetailID = 0;

        public List<TimeEntry> correctedTimes = new List<TimeEntry>();


        public void processTimeEntries(string ID)
        {
            try
            {
                if (ds != null)
                    ds.Clear();
                if (temp != null)
                    temp.Clear();
                //go through each status individually

                    //due to the size and nature of the data, we will do them by EntryType (6, 7, 8, 9). We ignore all draft time (0-5)
                    String SQL = "SELECT  EntryID, dbo.jfn_FormatClientCode(CliCode) as clicode,dbo.jfn_FormatMatterCode(MatCode) as matcode " +
                                 " ,EntryDate ,e.empname as empName ,BillableFlag ,ActualHoursWork ,Amount, EntryStatus " +
                                 " FROM TimeEntry t " +
                                 " inner join matter m on m.matsysnbr = t.MatterSysNbr " +
                                 " inner join client c on c.clisysnbr = ClientSysNbr " +
                                 " inner join employee e on e.empsysnbr = t.TimekeeperSysNbr " +
                                 " where t.entryid = " + ID;

                    ds = _jurisUtility.RecordsetFromSQL(SQL);
                    DataRow dr = ds.Tables[0].Rows[0];

                        TimeEntry te = new TimeEntry();
                        te.amount = Convert.ToDecimal(dr["Amount"].ToString().Trim());
                        te.hours = Convert.ToDecimal(dr["ActualHoursWork"].ToString().Trim());
                        te.ClientNo = dr["clicode"].ToString().Trim();
                        te.MatterNo = dr["matcode"].ToString().Trim();
                        te.ID = Convert.ToInt32(dr["EntryID"].ToString().Trim());
                        te.Date = DateTime.Parse(dr["EntryDate"].ToString().Trim()).ToString("MM/dd/yyyy");
                        te.oldEntryStatus = Convert.ToInt32(dr["EntryStatus"].ToString().Trim());
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


                    //go through each one and see if they are legit
                    switch (te.oldEntryStatus)
                    {
                        case 6:
                            {
                                ds.Clear();
                                temp.Clear();
                                //6 means they should be in tdb but NOT in unbilled or billedtime

                                    batchDetailID = 0;
                                    if (!isInTBD(te.ID))
                                        addToErrorsTime(0, "Recorded time was not in TimeBatchDetail. Setting to Draft", te);
                                    else //it IS in tbd but it should NOT be in unbilled or billed time
                                    {
                                        //unbilledtime
                                        if (isInUnbilledTime(batchDetailID))//if it IS
                                        {
                                            //see if it is in prebillfeeitem
                                            if (isOnPreBill(Convert.ToInt32(temp.Tables[0].Rows[0][0].ToString()), Convert.ToInt32(temp.Tables[0].Rows[0][1].ToString()))) //it IS in unbilled AND prebill
                                                addToErrorsTime(8, "Recorded time was on a PreBill. Setting to 'On PreBill'", te);
                                            else //it is NOT in PreBill
                                                addToErrorsTime(7, "Recorded time was on in UnbilledTime. Setting to Posted", te);
                                        }
                                        //billedtime
                                        else if (isBilled(batchDetailID)) //it IS in billed so it needs to be 9
                                            addToErrorsTime(9, "Recorded time was in BilledTime. Setting to Billed", te);

                                    }
                                    break;
                            }


                        case 7:
                            {
                                ds.Clear();
                                temp.Clear();
                                //7 means they should be in unbilledtime AND TBD but NOT in prebillfeeitem or billedtime

                                    batchDetailID = 0;
                                    if (!isInTBD(te.ID))
                                        addToErrorsTime(0, "Unbilled time was not in TimeBatchDetail. Setting to Draft", te);
                                    else //it IS in tbd but it needs to be in unbilledtime
                                    {
                                        //unbilledtime
                                        if (isInUnbilledTime(batchDetailID))//if it IS
                                        {
                                            //see if it is in prebillfeeitem
                                            if (isOnPreBill(Convert.ToInt32(temp.Tables[0].Rows[0][0].ToString()), Convert.ToInt32(temp.Tables[0].Rows[0][1].ToString()))) //it IS in unbilled AND prebill
                                                addToErrorsTime(8, "Unbilled time was on a PreBill. Setting to 'On PreBill'", te);
                                            // else it is NOT in PreBill so we do nothing because it is IN tbd and IN unbilledtime
                                        }
                                        else //it is in tbd but NOT unbilledtime so we see if it is in billedtime
                                        {
                                            //billedtime
                                            if (isBilled(batchDetailID)) //it IS in billed so it needs to be 9
                                                addToErrorsTime(9, "Recorded time was in BilledTime. Setting to Billed", te);
                                            else //not in billedtime, unbilledtime or prebill so it is a 6
                                                addToErrorsTime(6, "Unbilled time was in TimeBatchDetail but NOT UnbilledTime. Setting to Recorded", te);

                                        }


                                    }
                                    break;
                            }

                        case 8:
                            {
                                ds.Clear();
                                temp.Clear();
                                //8 means they should be in tdb AND unbilledtime AND prebillfeeitem

                                    batchDetailID = 0;
                                    if (!isInTBD(te.ID))
                                        addToErrorsTime(0, "Prebill time was not in TimeBatchDetail. Setting to Draft", te);
                                    else //it IS in tbd
                                    {
                                        //unbilledtime
                                        if (isInUnbilledTime(batchDetailID))//if it IS
                                        {
                                            //see if it is not in prebillfeeitem
                                            if (!isOnPreBill(Convert.ToInt32(temp.Tables[0].Rows[0][0].ToString()), Convert.ToInt32(temp.Tables[0].Rows[0][1].ToString()))) //it IS in unbilled AND prebill
                                                addToErrorsTime(7, "Prebill time was not on PreBill but WAS in UnbilledTime. Setting to Posted", te);
                                            //else do nothing because it is in all 3 tables
                                        }
                                        else //NOT in unbilledtime but it IS in tbd
                                        {
                                            //billedtime
                                            if (isBilled(batchDetailID)) //it IS in billed so it needs to be 9
                                                addToErrorsTime(9, "Prebill time was in BilledTime. Setting to Billed", te);
                                            else //not in unbilled, billed but IS in tbd
                                                addToErrorsTime(6, "Prebill time was not in UnbilledTime but IS in TimeBatchDetail. Setting to Recorded", te);
                                        }



                                    }
                                    break;
                            }

                        case 9:
                            {
                                ds.Clear();
                                temp.Clear();
                                //9 means it SHOULD be in TBD and billedtime but NOT in unbilledTime or PreBillFeeItem

                                    batchDetailID = 0;
                                    if (!isInTBD(te.ID))
                                        addToErrorsTime(0, "Billed time was not in TimeBatchDetail. Setting to Draft", te);
                                    else //it IS in tbd but it should NOT be in unbilled or billed time
                                    {
                                        //unbilledtime
                                        if (isInUnbilledTime(batchDetailID))//if it IS
                                        {
                                            //see if it is in prebillfeeitem
                                            if (isOnPreBill(Convert.ToInt32(temp.Tables[0].Rows[0][0].ToString()), Convert.ToInt32(temp.Tables[0].Rows[0][1].ToString()))) //it IS in unbilled AND prebill
                                                addToErrorsTime(8, "Billed time was on a PreBill. Setting to 'On PreBill'", te);
                                            else //it is NOT in PreBill
                                                addToErrorsTime(7, "Billed time was in UnbilledTime. Setting to Posted", te);
                                        }
                                        //billedtime
                                        else if (!isBilled(batchDetailID)) //it IS NOT in billed so it needs to be 0
                                            addToErrorsTime(0, "Billed time was not in BilledTime. Setting to Draft", te);
                                    }
                                    break;
                            }
                    }//end switch
            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
            }
        }


        private bool isInTBD(int entryID)
        {
            try
            {
                string SQL = "select tbdid from timebatchdetail where tbdid in (select tbdid from TimeEntryLink where EntryID = " + entryID + ")";
                ds = _jurisUtility.RecordsetFromSQL(SQL);
                if (ds.Tables[0].Rows.Count == 0)
                    return false;
                else
                {
                    batchDetailID = Convert.ToInt32(ds.Tables[0].Rows[0][0]);
                    return true;
                }
            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
                return false;
            }
        }

        private bool isInUnbilledTime(int entryID)
        {
            try
            {
            //utid = tbdid from timeentrylink
            string SQL = "select utbatch,UTRecNbr from unbilledtime where utid = " + entryID;
            DataSet dd = _jurisUtility.RecordsetFromSQL(SQL);
            temp = dd.Copy();
            if (dd.Tables[0].Rows.Count > 0) //it IS in unbilled so it needs to be 7 or 8
                return true;
            else
                return false;
            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
                return false;
            }
        }


        private bool isOnPreBill(int batchID, int recNo)
        {
            try
            {
            string SQL = "select * from PreBillFeeItem where PBFUTBatch = " + batchID + " and PBFUTRecNbr = " + recNo;
            DataSet df = _jurisUtility.RecordsetFromSQL(SQL);
            if (df.Tables[0].Rows.Count > 0) //it IS in prebillfeeitem, so it is an 8
                return true;
            else
                return false;
            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
                return false;
            }
        }

        private bool isBilled(int entryID)
        {
            try
            {
            //btid = tbdid from timeentrylink
            string SQL = "select * from billedtime where btid = " + entryID;
            DataSet dd = _jurisUtility.RecordsetFromSQL(SQL);
            if (dd.Tables[0].Rows.Count > 0) //it IS in billed so it needs to be 9
                return true;
            else
                return false;
            }
            catch (Exception ex1)
            {
                message = ex1.Message + "\r\n" + ex1.InnerException;
                return false;
            }
        }

        private void addToErrorsTime(int entryType, string message, TimeEntry entry)
        {
            entry.explanation = message;
            entry.newEntryStatus = entryType;
            correctedTimes.Add(entry);
        }


        public void updateTimeEntries(TimeEntry tt)
        {
                String SQL = "update timeentry set EntryStatus = " + tt.newEntryStatus + " where EntryID = " + tt.ID;
                _jurisUtility.ExecuteNonQuery(0, SQL);
        }


    }
}
