using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using MySql.Data.MySqlClient;
using System.Threading;
using System.Timers;

/// <summary>
/// Summary description for DbClass
/// </summary>
/// 

//SDEN STRUCT
struct textDetails
{
    public string message;
    public string environment;
};

struct allowedEnvironments
{
    public string telephoneNumber;
    public string environments;
};

struct textMessageList
{
    public string telephoneNumber;
    public string TextMessage;
};

struct environmentStatus
{
    public string environment;
    public string status;
};


public class DbClass
{
    //string connectionString = "server=10.210.166.32;database=AutoHarness;user id=eit;password=eit;persist security info=True";
    string connectionString = "";

    AlertSMS myAlertSMS = new AlertSMS();

    public DbClass()
    {
        //Get the db connection string from the webconfig
        connectionString = ConfigurationManager.AppSettings["connString"].ToString();

    }

    public bool GeneralInsertUpdate(string myInsertQuery)
    {
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(myInsertQuery);
        myCommand.Connection = myConnection;

        try
        {
            myConnection.Open();
            myCommand.ExecuteNonQuery();
            myCommand.Connection.Close();
        }

        catch (Exception ex)
        {
            //log the problem. if the insert doesn't work it will select again and keep trying
            myAlertSMS.MyEventLog("DB ERROR.stack trace :" + ex.Message);
            return false;
        }

        finally
        {
            myConnection.Close();
            myCommand.Connection.Close();
        }

        return true;

    }

    public int CheckPhoneNumber(string mySelectQuery, out string autohId)
    {
        int phoneNumberValid = 0;
        autohId = "";
        
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);

        //if the db is unavailable then it's going to crash here
        try
        {
            myConnection.Open();
            MySqlDataReader myReader = myCommand.ExecuteReader();

            while (myReader.Read())
            {
                phoneNumberValid = 1;
                autohId = myReader.GetString(0); //count of phone numbers
                break; // just take the first match. no functionality for multiple entries yet
            }

            myReader.Close();
        }
        catch (Exception e)
        {
            phoneNumberValid = -1;
            autohId = e.ToString();
            return phoneNumberValid;
        }
        finally
        {
            //myReader.Close();
            myConnection.Close();
        }

        return phoneNumberValid;

    }  //end ReadSmsInTable

    public bool ReadAutoHTable(string mySelectQuery, out string myStatusMessage, bool postChange)
    {
        myStatusMessage = "";

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        MySqlDataReader myReader;

        //if the db is unavailable then it's going to crash here
        try
        {
            myConnection.Open();
            myReader = myCommand.ExecuteReader();
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR .stack trace :" + ex.Message);
            return false;
        }

        try
        {
            while (myReader.Read())
            {
                if (postChange)
                {
                    myStatusMessage += "Your new AlertSMS status is:\r\n";
                }
                else
                {
                    myStatusMessage += "AlertSMS for Email:\r\n";

                    myStatusMessage += myReader.GetString(0) + "\r\n";   //email
                }

                //day
                if (Convert.ToInt32(myReader.GetString(1)) == 1)
                    myStatusMessage += "Day     ON";
                else
                    myStatusMessage += "Day     OFF";

                myStatusMessage += "\r\n";

                //night
                if (Convert.ToInt32(myReader.GetString(2)) == 1)
                    myStatusMessage += "Night   ON";   //day
                else
                    myStatusMessage += "Night   OFF";   //day

                myStatusMessage += "\r\n";

                //summary
                if (Convert.ToInt32(myReader.GetString(3)) == 1)
                    myStatusMessage += "Summary ON";   //day
                else
                    myStatusMessage += "Summary OFF";   //day

                myStatusMessage += "\r\n\r\n";

                break; //take the first only

            }
        }

        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR.stack trace :" + ex.Message);
            return false;
        }

        finally
        {
            myReader.Close();
            myConnection.Close();
        }

        if (!postChange)
        {
            myStatusMessage += "To change, send sms with type, status & carriage return i.e.\r\n\r\n"
                                + "Day OFF\r\nSummary OFF\r\n\r\n";
        }
        return true;

    }  //end ReadSmsInTable

    //public void GetDeadLetter(string mySelectQuery, out string smsIds2, out string smsIds3, out string messageResults)
    //{
    //    // -2 = phone number not recognised
    //    // -3 = phone number valid but the request not recognised

    //    smsIds2 = "";
    //    smsIds3 = "";
    //    messageResults = "";

    //    MySqlConnection myConnection = new MySqlConnection(connectionString);
    //    MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
    //    myConnection.Open();
    //    MySqlDataReader myReader;
    //    myReader = myCommand.ExecuteReader();

    //    try
    //    {
    //        while (myReader.Read())
    //        {
    //            string buff = myReader.GetString(4);

    //            if (buff == "-2") //invalid phone number
    //                smsIds2 += "\"" + myReader.GetString(0) + "\",";

    //            else if (buff == "-3") //invalid message content
    //                smsIds3 += "\"" + myReader.GetString(0) + "\",";

    //            messageResults += "Timestamp:" + myReader.GetString(3) + "\r\n";
    //            messageResults += "Phone Number:" + myReader.GetString(1) + "\r\n";

    //            if (myReader.GetString(5) != "#!##")
    //            {
    //                messageResults += "Name (if known):" + myReader.GetString(3) + "\r\n";

    //            }

    //            messageResults += "Message:" + myReader.GetString(2) + "\r\n";

    //            messageResults += "------------------------\r\n";

    //        }

    //        if (smsIds2.EndsWith(","))
    //            smsIds2 = smsIds2.Substring(0, smsIds2.Length - 1);

    //        if (smsIds3.EndsWith(","))
    //            smsIds3 = smsIds3.Substring(0, smsIds3.Length - 1);
    //        //send response message to sender confirming message invalid & common commands?
    //    }

    //    finally
    //    {
    //        myReader.Close();
    //        myConnection.Close();
    //    }
    //}

    public void GetSmsInStatReq(string mySelectQuery, out string smsIds, out string statusReq)
    {
        statusReq = "";
        smsIds = "";

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        myConnection.Open();
        MySqlDataReader myReader;
        myReader = myCommand.ExecuteReader();

        try
        {
            while (myReader.Read())
            {
                smsIds += "\"" + myReader.GetString(0) + "\",";   //SmsInId
                statusReq += myReader.GetString(1) + "#";
                statusReq += myReader.GetString(5) + "|";

            }

            if (smsIds.EndsWith(","))
                smsIds = smsIds.Substring(0, smsIds.Length - 1);


        }

        finally
        {
            myReader.Close();
            myConnection.Close();
        }

    }  //end  ReadSmsOutTable

    public void ReadSmsOutTable(string mySelectQuery, out string[,] smsOutArr, out int arrSize)
    {
        int maxBatchSize = 20;
        smsOutArr = new string[maxBatchSize, 4]; //rows, elements in row

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        myConnection.Open();
        MySqlDataReader myReader;
        myReader = myCommand.ExecuteReader();

        int i = 0;

        try
        {
            while (myReader.Read())
            {
                if (i == 20)
                {
                    break;
                }

                smsOutArr[i, 0] += myReader.GetString(0);   //SmsOutId
                smsOutArr[i, 1] += myReader.GetString(1);   //number
                smsOutArr[i, 2] += myReader.GetString(2);   //Message
                smsOutArr[i, 3] += myReader.GetString(3);   //timestamp
                //smsOutArr[i, 4] += myReader.GetString(4);   //sent or not -1=not sent; 0=fail; 1=success
                i++;
            }

            arrSize = i;

        }

        finally
        {
            myReader.Close();
            myConnection.Close();
        }

    }  //end  ReadSmsOutTable

    public bool ReadEitMHSTables(string mySelectQuery, out string[] messageArr, out string response_message)
    {
        messageArr = new string[10];
        response_message = "";


        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        myConnection.Open();
        MySqlDataReader myReader;
        myReader = myCommand.ExecuteReader();

        try
        {
            while (myReader.Read())
            {

                messageArr[0] = myReader.GetString(0);   //id
                messageArr[1] = myReader.GetString(1);   //environment
                messageArr[2] = myReader.GetString(2);   //message type
                messageArr[3] = myReader.GetString(3);   //start time
                messageArr[4] = myReader.GetString(4);   //end time
                messageArr[5] = myReader.GetString(5);   // response time (sec)
                messageArr[6] = myReader.GetString(6);   // app availability
                messageArr[7] = myReader.GetString(7);   // responses received
                messageArr[8] = myReader.GetString(8);   // error info


                response_message = myReader.GetString(9); // reponse message


                break;
            }


        }

        catch
        {
            return false;
        }

        finally
        {
            myReader.Close();
            myConnection.Close();
        }

        return true;

    }  //end  ReadSmsOutTable

    //Start of text alert functions SDEN

    public string GeneratetextAlert(string phoneNumberStatus)
    {
        DateTime dateNow = DateTime.Now;
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlDataAdapter autoHDataAdapter;
        DataSet autoHDataSet;
        Boolean isItTheWeekend = false;
        DateTime alertTime;
        String textMessage = "";
        DateTime date = DateTime.Now; 

        //SQL queries

        //Change HERE to check for WEEKEND on STATUS and summary messages
        string autoHQuery = "";
        string autoHStatusQuery = "";
        Boolean weekend = false;

        if ((date.DayOfWeek.ToString() == "Saturday") || (date.DayOfWeek.ToString() == "Sunday"))
        {
            autoHQuery = "select PhoneNumber,EnvironmentsAllowed,weekdayAlertTime,weekendAlertTime,summaryMessageLastSent from AutoH where SummaryAlertText = 1 and WeekendAlert=1;";
            
        }
        else
        {
            autoHQuery = "select PhoneNumber,EnvironmentsAllowed,weekdayAlertTime,weekendAlertTime,summaryMessageLastSent from AutoH where SummaryAlertText = 1 and DayAlertText=1;";
            
        }

        autoHStatusQuery = "select PhoneNumber,EnvironmentsAllowed,weekdayAlertTime,weekendAlertTime,summaryMessageLastSent from AutoH where phoneNumber=" + phoneNumberStatus + ";";
        
        //Determine what type of message we need to send
        
        //First check if we need to a status message 
        //then check if we need to send a summary message
        if (phoneNumberStatus != "")
        {
            //This is a status message

            //Get the times to send the summary and the last time it was sent
            try
            {
                autoHDataAdapter = new MySqlDataAdapter(autoHStatusQuery, myConnection);
                autoHDataSet = new DataSet();
                autoHDataAdapter.Fill(autoHDataSet, "autoH");
            }
            catch (Exception ex)
            {
                myAlertSMS.MyEventLog("DB ERROR retreiving environments for TextAlert" + ex.Message);
                return "ERROR999";
            }
            finally
            {
                myConnection.Close();
            }
            
            //Loop through and see if each user needs a summary message to be sent
            DataView autoHRows = new DataView(autoHDataSet.Tables["autoH"]);

            foreach (DataRowView autoHRow in autoHRows)
            {
                textMessage = textMessage + GenerateSummary(autoHRow["PhoneNumber"].ToString(), autoHRow["EnvironmentsAllowed"].ToString());
            }
        }
        else
        {
            //Get the times to send the summary and the last time it was sent
            try
            {
                autoHDataAdapter = new MySqlDataAdapter(autoHQuery, myConnection);
                autoHDataSet = new DataSet();
                autoHDataAdapter.Fill(autoHDataSet, "autoH");
            }
            catch (Exception ex)
            {
                myAlertSMS.MyEventLog("DB ERROR retreiving environments for TextAlert.stack trace :" + ex.Message);
                return "";
            }
            finally
            {
                myConnection.Close();
            }

            //Loop through and see if each user needs a summary message to be sent
            DataView autoHRows = new DataView(autoHDataSet.Tables["autoH"]);

            foreach (DataRowView autoHRow in autoHRows)
            {
                string day = dateNow.DayOfWeek.ToString();
                //Determine if it is the weekend or weekday
                if ((dateNow.DayOfWeek.ToString() == "Saturday") || (dateNow.DayOfWeek.ToString() == "Sunday"))
                {
                    isItTheWeekend = true;

                    //Concatentate the date of now with the time of the summary message
                    DateTime tempDate = DateTime.Parse(autoHRow["weekendAlertTime"].ToString());
                    String tempString = DateTime.Now.ToShortDateString() + " " + tempDate.ToString("HH:mm:ss");

                    alertTime = DateTime.Parse(tempString);
                }
                else
                {
                    isItTheWeekend = false;

                    //Concatentate the date of now with the time of the summary message
                    DateTime tempDate = DateTime.Parse(autoHRow["weekdayAlertTime"].ToString());
                    String tempString = DateTime.Now.ToShortDateString() + " " + tempDate.ToString("HH:mm:ss");

                    alertTime = DateTime.Parse(tempString);

                }
                DateTime summaryMessageLastSent = DateTime.Parse(autoHRow["summaryMessageLastSent"].ToString());
                DateTime alertTimeMinusOne = alertTime.AddDays(-1);

                //Check if time now is greater than weekday alert time 
                //and that summaryMessageLastSent is yesterday

                //if ((dateNow > alertTime) && ((summaryMessageLastSent.Day == alertTimeMinusOne.Day) && (summaryMessageLastSent.Month == alertTimeMinusOne.Month)))
                //if ((dateNow > alertTime) && (dateNow <= alertTime.AddHours(1)) && ((summaryMessageLastSent.Day <= alertTimeMinusOne.Day) && (summaryMessageLastSent.Month <= alertTimeMinusOne.Month))) removed 05/06/2010
                if ((dateNow > alertTime) && (dateNow <= alertTime.AddHours(1)) && (summaryMessageLastSent <= alertTimeMinusOne))
                {
                    //A summary message needs to be sent for user
                    textMessage = textMessage + GenerateSummary(autoHRow["PhoneNumber"].ToString(), autoHRow["EnvironmentsAllowed"].ToString());

                    //Update summaryMessageLastSent in DB to be AlertTime
                    UpdateAutoHMessageSummary(autoHRow["PhoneNumber"].ToString(), alertTime);

                }
                else
                {
                    //No Summery message needed

                }

            }

    
            
        }  //end for each autoH row

        //Check if a summary has been sent if not do an ad-hoc
        if (textMessage == "")
        {
            textMessage = GenerateAdHoc();
        }

        //textMessage = textMessage;

        if (textMessage != "")
        {
            textMessage = textMessage.Substring(0, textMessage.Length - 1);

            myAlertSMS.MyEventLog("Test Message being sent :" + textMessage);
        }

        return textMessage;


    }  //end GenerateTextAlert
    
    public string GenerateSummary(String PhoneNumber, String EnvironmentsAllowed)
    {
        textDetails[] textDetails = new textDetails[100];
        textMessageList[] mainTextMessageList = new textMessageList[20];
        int messageNumber = 0;
        int telephoneNumberCounter = 0;
        string previousEnvironment = "";
        bool firstIteration = true;
        DateTime date = DateTime.Now;
        allowedEnvironments[] environments = new allowedEnvironments[20];
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlDataAdapter environmentDataAdapter;
        DataSet environmentDataSet;
        MySqlDataAdapter resultsDataAdapter;
        DataSet resultsDataSet;

        //SQL queries
        /*string resultsQuery = "select * from ( " +
           "SELECT  t1.sort_order,t1.proc_type, a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=0,1,0))  StatusBad,0 StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2, types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
           "FROM eitmhsstats a1,applicationnames a2,types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=0,1,0)) StatusBad,0 StatusNone,(sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2,types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
           "FROM eitmhsstats a1, applicationnames a2, types t1 WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) " +
           "AND a1.application_id = a2.application_id and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          ") x order by 6,2,3,4,1";*/
        string resultsQuery = "SELECT tab,  type_id,  type_name, text_alert, sum(StatusGood),  Sum(StatusBad),  Sum(StatusNone),  round(Sum(StatusGood) / sum(StatusGood + StatusBad + StatusNone), 2) As percentage FROM (SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id,  t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF(a1.app_availability = 0, 1, 0) StatusBad,  0 StatusNone,  t1.fails_needed,  t1.type_name FROM applicationstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) x UNION SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id, t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF (a1.app_availability = -1,  1,  IF(a1.app_availability = 2, 1, 0)  ) StatusBad,  IF(a1.app_availability = 0, 1, 0) StatusNone,  t1.fails_needed,  t1.type_name FROM eitmhsstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) y) Z  WHERE ROWNUM <= Fails_Needed GROUP BY tab, type_id, type_name, text_alert";
        string environmentQuery = "select environment from applicationstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) UNION select environment from EITMHsstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) order by Environment";


        //Get the environmments tested in the period
        try
        {
            environmentDataAdapter = new MySqlDataAdapter(environmentQuery, myConnection);
            environmentDataSet = new DataSet();
            environmentDataAdapter.Fill(environmentDataSet, "environments");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving environments for TextAlert.stack trace :" + ex.Message);
            return "";
        }
        finally
        {
            myConnection.Close();
        }
        
        //Get the results data for the time period
        try
        {
            resultsDataAdapter = new MySqlDataAdapter(resultsQuery, myConnection);
            resultsDataSet = new DataSet();
            resultsDataAdapter.Fill(resultsDataSet, "Results");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving results for TextAlert" + ex.Message);
            return "";
        }
        finally
        {
            myConnection.Close();
        }

        //Loop through the each environment tested
        DataView environmentRows = new DataView(environmentDataSet.Tables["environments"]);

        foreach (DataRowView environmentRow in environmentRows)
        {
            //filter to find the number passed and failled tests
            DataView resultsFailedRows = new DataView(resultsDataSet.Tables["results"]);
            //resultsFailedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage <1";
            resultsFailedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage <1 AND text_alert =1";

            DataView resultsPassedRows = new DataView(resultsDataSet.Tables["results"]);
            //resultsPassedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage =1";
            resultsPassedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage =1 AND text_alert =1";
            
            int resultsFailedLength = resultsFailedRows.Count;
            int resultsPassedLength = resultsPassedRows.Count;
            Boolean dataWrittenForFails = false;

            int numberOfFailsWritten = 0;

            //Check if there where some failures in the environment
            if (resultsFailedLength > 0 && resultsPassedLength != 0)
            {
                //some tests have passed and some have failed
                string failureTime = "";
                int entry_id = 0;

                //Loop through failures and write them to the text message
                foreach (DataRowView resultRow in resultsFailedRows)
                {

                    if (numberOfFailsWritten <= 6)
                    {
                        //Check the time it failed
                        Boolean historyCheck = true;
                        try
                        {
                            //Reset failure time
                            failureTime = "";
                            
                            historyCheck = CheckApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], ref failureTime, ref entry_id);
                        }
                        catch (Exception ex)
                        {
                            myAlertSMS.MyEventLog("checkfailure error " + ex.Message);
                        }

                        if ((historyCheck==false))
                        {
                            textDetails[messageNumber].message = resultRow["type_name"] + " F (@ " + failureTime + ")";
                            textDetails[messageNumber].environment = (string)resultRow["tab"];

                            dataWrittenForFails = true;

                            //Increment the number of messages
                            messageNumber++;

                            //Increment failures written
                            numberOfFailsWritten++;

                        }
                    }
                    else
                    {
                        //There are more than 6 failures in env,
                        //Limit message
                        textDetails[messageNumber].message = "+ more failures";
                        textDetails[messageNumber].environment = (string)resultRow["tab"];

                        messageNumber++;
                        break;
                    }

                }

                if ((numberOfFailsWritten <= 6) && (dataWrittenForFails ==true))
                {
                    //Output that all other tests have passed since we have already output failed ones
                    textDetails[messageNumber].message = "All others passed";
                    textDetails[messageNumber].environment = (string)environmentRow["environment"];

                    //Increment the number of messages
                    messageNumber++;
                }

            }
            //else
            if ((dataWrittenForFails == false) || (resultsFailedLength == 0 && resultsPassedLength > 0))
            {
                //All tests have passed, write this to the text
                textDetails[messageNumber].message = "All tests passed";
                textDetails[messageNumber].environment = (string)environmentRow["environment"];

                //Increment the number of messages
                messageNumber++;
            }
            else if (resultsFailedLength > 0 && resultsPassedLength == 0)
            {
                //All tests have failed, write this to the text
                textDetails[messageNumber].message = "All tests failed";
                textDetails[messageNumber].environment = (string)environmentRow["environment"];

                //Increment the number of messages
                messageNumber++;
            }

        }  //end for each environment

        //Retrieve the telephone Numbers and allowed environments
        //RetrieveAllowedEnvs(0, ref environments);

        //Loop through the allowed environments and telephone numbers

        telephoneNumberCounter = 0;

       // while (environments[telephoneNumberCounter].telephoneNumber != null)
       // {
            //mainTextMessageList[telephoneNumberCounter].telephoneNumber = environments[telephoneNumberCounter].telephoneNumber;
            mainTextMessageList[telephoneNumberCounter].telephoneNumber = PhoneNumber;
            //Loop through text message details and determine if user should receive message
            //based on enviromentsments allowed

            for (int x = 0; x < messageNumber; x++)
            {
                //Check if environment is blocked from text messages
                //bool environmentBlocked = CheckEnvironmentBlock(textDetails[x].environment);

                //if environment allowed add message
                //if (environments[telephoneNumberCounter].environments.IndexOf(textDetails[x].environment) >= 0)
                if ((EnvironmentsAllowed.IndexOf(textDetails[x].environment) >= 0) )//&& environmentBlocked ==false)
                {
                    //environment allowed add message

                    if (textDetails[x].environment != previousEnvironment)
                    {
                        if (firstIteration != true)
                        {
                            mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\n";
                        }
                        firstIteration = false;

                        mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].environment +"\r\n";

                        mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "-------\r\n";

                    }

                    mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].message +"\r\n";

                    previousEnvironment = textDetails[x].environment;
                }
            }

            //add generated time.
            if (mainTextMessageList[telephoneNumberCounter].TextMessage.Length != 0)
            {
                mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\nGenerated: " + date.ToShortTimeString() + " " + date.ToShortDateString();
            }
            
         
            telephoneNumberCounter++;
       // }


        //Turn array into large string
        string fullTextMessageString = "";

        for (int y = 0; y < telephoneNumberCounter; y++)
        {
            fullTextMessageString = fullTextMessageString + mainTextMessageList[y].telephoneNumber + "|" + mainTextMessageList[y].TextMessage + "#";
        }




        
        return fullTextMessageString;
 

    }  //end GenerateSummary
    public string GenerateSummaryOld(String PhoneNumber, String EnvironmentsAllowed)
    {
        textDetails[] textDetails = new textDetails[100];
        textMessageList[] mainTextMessageList = new textMessageList[20];
        int messageNumber = 0;
        int telephoneNumberCounter = 0;
        string previousEnvironment = "";
        bool firstIteration = true;
        DateTime date = DateTime.Now;
        allowedEnvironments[] environments = new allowedEnvironments[20];
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlDataAdapter environmentDataAdapter;
        DataSet environmentDataSet;
        MySqlDataAdapter resultsDataAdapter;
        DataSet resultsDataSet;

        //SQL queries
        string resultsQuery = "select * from ( " +
           "SELECT  t1.sort_order,t1.proc_type, a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=0,1,0))  StatusBad,0 StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2, types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
           "FROM eitmhsstats a1,applicationnames a2,types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=0,1,0)) StatusBad,0 StatusNone,(sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2,types t1 " +
           "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
           "and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          " union " +
           "SELECT  t1.sort_order,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
           "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
           "FROM eitmhsstats a1, applicationnames a2, types t1 WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) " +
           "AND a1.application_id = a2.application_id and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
          ") x order by 6,2,3,4,1";

        string environmentQuery = "select environment from applicationstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) UNION select environment from EITMHsstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) order by Environment";


        //Get the environmments tested in the period
        try
        {
            environmentDataAdapter = new MySqlDataAdapter(environmentQuery, myConnection);
            environmentDataSet = new DataSet();
            environmentDataAdapter.Fill(environmentDataSet, "environments");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving environments for TextAlert" + ex.Message);
            return "";
        }
        finally
        {
            myConnection.Close();
        }

        //Get the results data for the time period
        try
        {
            resultsDataAdapter = new MySqlDataAdapter(resultsQuery, myConnection);
            resultsDataSet = new DataSet();
            resultsDataAdapter.Fill(resultsDataSet, "Results");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving results for TextAlert" + ex.Message);
            return "";
        }
        finally
        {
            myConnection.Close();
        }

        //Loop through the each environment tested
        DataView environmentRows = new DataView(environmentDataSet.Tables["environments"]);

        foreach (DataRowView environmentRow in environmentRows)
        {
            //filter to find the number passed and failled tests
            DataView resultsFailedRows = new DataView(resultsDataSet.Tables["results"]);
            resultsFailedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage =0";

            DataView resultsPassedRows = new DataView(resultsDataSet.Tables["results"]);
            resultsPassedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage >0";

            int resultsFailedLength = resultsFailedRows.Count;
            int resultsPassedLength = resultsPassedRows.Count;

            int numberOfFailsWritten = 0;

            //Check if there where some failures in the environment
            if (resultsFailedLength > 0 && resultsPassedLength != 0)
            {
                //some tests have passed and some have failed
                string failureTime = "";
                int entry_id = 0;

                //Loop through failures and write them to the text message
                foreach (DataRowView resultRow in resultsFailedRows)
                {

                    if (numberOfFailsWritten <= 6)
                    {
                        //Check the time it failed
                        try
                        {
                            CheckApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], ref failureTime, ref entry_id);
                        }
                        catch (Exception ex)
                        {
                            myAlertSMS.MyEventLog("checkfailure error " + ex.Message);
                        }

                        if (failureTime != "")
                        {
                            textDetails[messageNumber].message = resultRow["type_name"] + " F (@ " + failureTime + ")";
                            textDetails[messageNumber].environment = (string)resultRow["tab"];
                        }
                        else
                        {
                            textDetails[messageNumber].message = resultRow["type_name"] + " F";
                            textDetails[messageNumber].environment = (string)resultRow["tab"];
                        }

                        //Increment the number of messages
                        messageNumber++;

                        //Increment failures written
                        numberOfFailsWritten++;
                    }
                    else
                    {
                        //There are more than 6 failures in env,
                        //Limit message
                        textDetails[messageNumber].message = "+ more failures";
                        textDetails[messageNumber].environment = (string)resultRow["tab"];

                        messageNumber++;
                        break;
                    }

                }

                if (numberOfFailsWritten <= 6)
                {
                    //Output that all other tests have passed since we have already output failed ones
                    textDetails[messageNumber].message = "All others passed";
                    textDetails[messageNumber].environment = (string)environmentRow["environment"];

                    //Increment the number of messages
                    messageNumber++;
                }

            }
            else if (resultsFailedLength == 0 && resultsPassedLength > 0)
            {
                //All tests have passed, write this to the text
                textDetails[messageNumber].message = "All tests passed";
                textDetails[messageNumber].environment = (string)environmentRow["environment"];

                //Increment the number of messages
                messageNumber++;
            }
            else if (resultsFailedLength > 0 && resultsPassedLength == 0)
            {
                //All tests have failed, write this to the text
                textDetails[messageNumber].message = "All tests failed";
                textDetails[messageNumber].environment = (string)environmentRow["environment"];

                //Increment the number of messages
                messageNumber++;
            }

        }  //end for each environment

        //Retrieve the telephone Numbers and allowed environments
        //RetrieveAllowedEnvs(0, ref environments);

        //Loop through the allowed environments and telephone numbers

        telephoneNumberCounter = 0;

        // while (environments[telephoneNumberCounter].telephoneNumber != null)
        // {
        //mainTextMessageList[telephoneNumberCounter].telephoneNumber = environments[telephoneNumberCounter].telephoneNumber;
        mainTextMessageList[telephoneNumberCounter].telephoneNumber = PhoneNumber;
        //Loop through text message details and determine if user should receive message
        //based on enviromentsments allowed

        for (int x = 0; x < messageNumber; x++)
        {
            //Check if environment is blocked from text messages
            //bool environmentBlocked = CheckEnvironmentBlock(textDetails[x].environment);

            //if environment allowed add message
            //if (environments[telephoneNumberCounter].environments.IndexOf(textDetails[x].environment) >= 0)
            if ((EnvironmentsAllowed.IndexOf(textDetails[x].environment) >= 0))//&& environmentBlocked ==false)
            {
                //environment allowed add message

                if (textDetails[x].environment != previousEnvironment)
                {
                    if (firstIteration != true)
                    {
                        mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\n";
                    }
                    firstIteration = false;

                    mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].environment + "\r\n";

                    mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "-------\r\n";

                }

                mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].message + "\r\n";

                previousEnvironment = textDetails[x].environment;
            }
        }

        //add generated time.
        if (mainTextMessageList[telephoneNumberCounter].TextMessage.Length != 0)
        {
            mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\nGenerated: " + date.ToShortTimeString() + " " + date.ToShortDateString();
        }


        telephoneNumberCounter++;
        // }


        //Turn array into large string
        string fullTextMessageString = "";

        for (int y = 0; y < telephoneNumberCounter; y++)
        {
            fullTextMessageString = fullTextMessageString + mainTextMessageList[y].telephoneNumber + "|" + mainTextMessageList[y].TextMessage + "#";
        }





        return fullTextMessageString;


    }  //end GenerateSummaryOld

    public string GenerateAdHoc()
    {
        textDetails[] textDetails = new textDetails[50];
        textMessageList[] mainTextMessageList = new textMessageList[20];
        int messageNumber = 0;
        int telephoneNumberCounter = 0;
        int entry_id = 0;
        string previousEnvironment = "";
        string failureTime = "";
        bool firstIteration = true;
        DateTime date = DateTime.Now;
        allowedEnvironments[] environments = new allowedEnvironments[20];
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlDataAdapter environmentDataAdapter;
        DataSet environmentDataSet;
        MySqlDataAdapter resultsDataAdapter;
        DataSet resultsDataSet;
        string statusChanged  = "";
        bool environmentHeaderWritten = false;

        //SQL queries
        //SELECT tab,type_id,type_name,sum(StatusGood),Sum(StatusBad),Sum(StatusNone),round(Sum(StatusGood) / sum(StatusGood + StatusBad + StatusNone), 2) as percentage FROM (SELECT tab, type_id, StatusGood, StatusBad, StatusNone, fails_needed, type_name, @running   := if(@previous_id <> type_id, 0, @running) + 1    AS ROWNUM, @previous_id   := type_id   FROM (SELECT a1.start_time,a1.Environment Tab,a2.type_id,IF(a1.app_availability = 1, 1, 0) StatusGood,IF(a1.app_availability = 0, 1, 0) StatusBad,0 StatusNone,t1.fails_needed,t1.type_name    FROM applicationstats a1, applicationnames a2, types t1   WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND  DATE_SUB(NOW(), INTERVAL 2 MINUTE)AND a1.application_id = a2.application_id AND a2.type_id = t1.type_id  ORDER BY tab, type_id, start_time DESC) x UNION SELECT tab, type_id, StatusGood, StatusBad, StatusNone, fails_needed, type_name, @running   := if(@previous_id <> type_id, 0, @running) + 1    AS ROWNUM, @previous_id   := type_id   FROM (SELECT a1.start_time,a1.Environment Tab,a2.type_id,IF(a1.app_availability = 1, 1, 0) StatusGood,IF (a1.app_availability = -1,    1,    IF(a1.app_availability = 2, 1, 0))   StatusBad,IF(a1.app_availability = 0, 1, 0) StatusNone,t1.fails_needed,t1.type_name    FROM eitmhsstats a1, applicationnames a2, types t1   WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND  DATE_SUB(NOW(), INTERVAL 2 MINUTE)AND a1.application_id = a2.application_id AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) y) Z WHERE ROWNUM <= Fails_Needed GROUP BY tab, type_id, type_name

        /*string resultsQuery = "select * from ( " +
          "SELECT  t1.sort_order, t1.text_alert,t1.proc_type, a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
          "sum(IF(a1.app_availability=0,1,0))  StatusBad,0 StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2, types t1 " +
          "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 30 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
          "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
         " union " +
          "SELECT  t1.sort_order, t1.text_alert,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
          "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
          "FROM eitmhsstats a1,applicationnames a2,types t1 " +
          "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 30 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
          "and a2.type_id = t1.type_id and t1.proc_type != 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
         " union " +
          "SELECT  t1.sort_order, t1.text_alert,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
          "sum(IF(a1.app_availability=0,1,0)) StatusBad,0 StatusNone,(sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+sum(IF(a1.app_availability=0,1,0))) percentage FROM applicationstats a1, applicationnames a2,types t1 " +
          "WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) AND a1.application_id = a2.application_id " +
          "and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
         " union " +
          "SELECT  t1.sort_order, t1.text_alert,t1.proc_type,a2.type_id,t1.type_name,max(t1.fails_needed) failsneeded,a1.Environment Tab,sum(IF(a1.app_availability=1,1,0)) StatusGood, " +
          "sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) StatusBad,sum(IF(a1.app_availability=0,1,0)) StatusNone,  (sum(IF(a1.app_availability=1,1,0)))/(sum(IF(a1.app_availability=1,1,0))+ sum(IF(a1.app_availability=-1,1,IF(a1.app_availability=2,1,0))) + sum(IF(a1.app_availability=0,1,0))) percentage " +
          "FROM eitmhsstats a1, applicationnames a2, types t1 WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) " +
          "AND a1.application_id = a2.application_id and a2.type_id = t1.type_id and t1.proc_type = 3 group by t1.sort_order,t1.proc_type, type_id, type_name, Tab " +
         ") x order by 7,3,4,5,1";*/
        string resultsQuery = "SELECT tab,  type_id,  type_name, text_alert, sum(StatusGood),  Sum(StatusBad),  Sum(StatusNone),  round(Sum(StatusGood) / sum(StatusGood + StatusBad + StatusNone), 2) As percentage FROM (SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id,  t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF(a1.app_availability = 0, 1, 0) StatusBad,  0 StatusNone,  t1.fails_needed,  t1.type_name FROM applicationstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) x UNION SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id, t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF (a1.app_availability = -1,  1,  IF(a1.app_availability = 2, 1, 0)  ) StatusBad,  IF(a1.app_availability = 0, 1, 0) StatusNone,  t1.fails_needed,  t1.type_name FROM eitmhsstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) y) Z  WHERE ROWNUM <= Fails_Needed GROUP BY tab, type_id, type_name, text_alert";
        //string resultsQuery = "SELECT tab,  type_id,  type_name, text_alert, sum(StatusGood),  Sum(StatusBad),  Sum(StatusNone),  round(Sum(StatusGood) / sum(StatusGood + StatusBad + StatusNone), 2) As percentage FROM (SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id,  t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF(a1.app_availability = 0, 1, 0) StatusBad,  0 StatusNone,  t1.fails_needed,  t1.type_name FROM applicationstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) x UNION SELECT tab,  type_id, text_alert, StatusGood,  StatusBad,  StatusNone,  fails_needed,  type_name,  @running := if(@previous_id <> type_id, 0, @running) + 1 AS ROWNUM,  @previous_id := type_id FROM (SELECT a1.start_time,  a1.Environment Tab,  a2.type_id, t1.text_alert, IF(a1.app_availability = 1, 1, 0) StatusGood,  IF (a1.app_availability = -1,  1,  IF(a1.app_availability = 2, 1, 0)  ) StatusBad,  IF(a1.app_availability = 0, 1, 0) StatusNone,  t1.fails_needed,  t1.type_name FROM eitmhsstats a1, applicationnames a2, types t1  WHERE start_time BETWEEN DATE_SUB(NOW(), INTERVAL 12 HOUR) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE)  AND a1.application_id = a2.application_id  AND a2.type_id = t1.type_id ORDER BY tab, type_id, start_time DESC) y) Z  WHERE ROWNUM <= Fails_Needed GROUP BY tab, type_id, type_name, text_alert";
        string environmentQuery = "select environment from applicationstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) UNION select environment from EITMHsstats where start_time BETWEEN DATE_SUB(NOW(), INTERVAL 32 MINUTE) AND DATE_SUB(NOW(), INTERVAL 2 MINUTE) order by Environment";


        //Get the environmments tested in the period
        try
        {
            environmentDataAdapter = new MySqlDataAdapter(environmentQuery, myConnection);
            environmentDataSet = new DataSet();
            environmentDataAdapter.Fill(environmentDataSet, "environments");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving environments for TextAlert" + ex.Message);
            throw new Exception(ex.Message);
            
        }
        finally
        {
            myConnection.Close();
        }

        //Get the results data for the time period
        try
        {
            resultsDataAdapter = new MySqlDataAdapter(resultsQuery, myConnection);
            resultsDataSet = new DataSet();
            resultsDataAdapter.Fill(resultsDataSet, "Results");
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR retreiving results for TextAlert.stack trace :" + ex.Message);
            throw new Exception(ex.Message);
            
        }
        finally
        {
            myConnection.Close();
        }

        //Loop through the each environment tested
        DataView environmentRows = new DataView(environmentDataSet.Tables["environments"]);

        foreach (DataRowView environmentRow in environmentRows)
        {
            //filter to find the number passed and failled tests
            DataView resultsFailedRows = new DataView(resultsDataSet.Tables["results"]);
            resultsFailedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage = 0";

            DataView resultsPassedRows = new DataView(resultsDataSet.Tables["results"]);
            resultsPassedRows.RowFilter = "tab = '" + environmentRow["environment"] + "' AND percentage = 1";

            int resultsFailedLength = resultsFailedRows.Count;
            int resultsPassedLength = resultsPassedRows.Count;

            //Check if there where some failures in the environment
            if (resultsFailedLength > 0 || resultsPassedLength > 0)//&& resultsPassedLength != 0)
            {
                //some tests have passed and some have failed

                //Loop through failures and write them to the text message
                foreach (DataRowView resultRow in resultsFailedRows)
                {
                    entry_id = 0;

                    //Check if the application was already failed
                    if (CheckApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], ref failureTime, ref entry_id) == false)
                    {
                        //Application has previously failed
                        textDetails[messageNumber].message = resultRow["type_name"] + " F (was F)";
                        textDetails[messageNumber].environment = (string)resultRow["tab"];
                    }
                    else
                    {
                        //Application has not previously failed
                        //add message if text alert is allowed for type
                        if (resultRow["text_alert"].ToString() == "1")
                        {
                            textDetails[messageNumber].message = resultRow["type_name"] + " F (was P)";
                            textDetails[messageNumber].environment = (string)resultRow["tab"];
                            
                            //Update status change for env...Somehow
                            //Add the environment name to delimited string of envs where status has changed
                            statusChanged = statusChanged + (string)resultRow["tab"] + "#";

                            //Increment the number of messages
                            messageNumber++;

                        }
                        //Update Datebase with the failure
                        UpdateApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], 0, 0);

                    }
                }

                //Loop through success and write them to the text message
                foreach (DataRowView resultRow in resultsPassedRows)
                {
                    //Check if the application was previuolsy failed
                    if (CheckApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], ref failureTime, ref entry_id) == false)
                    {
                        string textAlert = resultRow["text_alert"].ToString();
                        
                        //Application has previously failed and has now passed
                        //check if text alert is allowed
                        if (textAlert == "1")
                        {                            
                            textDetails[messageNumber].message = resultRow["type_name"] + " P (was F)";
                            textDetails[messageNumber].environment = (string)resultRow["tab"];

                            //Add the environment name to delimited string of envs where status has changed
                            statusChanged = statusChanged + (string)resultRow["tab"] + "#";

                            //Increment the number of messages
                            messageNumber++;
                        }
                        
                        //Update Datebase with the success
                        UpdateApplicationHistory((int)resultRow["type_id"], (string)resultRow["tab"], entry_id, 1);
                    }

                } //end for each success app
            } //end if there are failures
       
        }  //end for each environment

        //Retrieve the telephone Numbers and allowed environments
        //Check the time if between 10pm and 6am then retreive nightalert

        //Move date check into RetrieveAllowedEnvs
        // ((date.Hour >=22 && date.Hour <=24) ||(date.Hour >=00 && date.Hour <=5))
        //{
            //RetrieveAllowedEnvs(2, ref environments);
        //}
        //else
        //{
            RetrieveAllowedEnvs(1, ref environments);
        //}


        //Loop through the allowed environments and telephone numbers

        telephoneNumberCounter = 0;

        while (environments[telephoneNumberCounter].telephoneNumber != null)
        {
            mainTextMessageList[telephoneNumberCounter].telephoneNumber = environments[telephoneNumberCounter].telephoneNumber;
            firstIteration = true;
            previousEnvironment = "";

            //Loop through text message details and determine if user should receive message
            //based on enviromentsments allowed

            for (int x = 0; x < messageNumber; x++)
            {
                //Check if environment is blocked from text messages
                bool environmentBlocked = CheckEnvironmentBlock(textDetails[x].environment);

                //if environment allowed  and status has changed for env 
                //and environment not blocked add message
                if ((environments[telephoneNumberCounter].environments.IndexOf(textDetails[x].environment) >= 0) &&
                (statusChanged.IndexOf(textDetails[x].environment) >= 0) && environmentBlocked==false)
                {
                    if (textDetails[x].environment != previousEnvironment)
                    {
                        environmentHeaderWritten = false;
                    }
                    else
                    {
                        environmentHeaderWritten = true;
                    }
                    
                    //Check if env changed
                    if (environmentHeaderWritten == false)
                    {
                        if (firstIteration != true)
                        {
                            mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\n";
                        }
                        firstIteration = false;

                        mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].environment + "\r\n";

                        mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "-------\r\n";

                    }

                    mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + textDetails[x].message + "\r\n";

                    previousEnvironment = textDetails[x].environment;
                }
            }

            //add generated time if there is a message
            if (mainTextMessageList[telephoneNumberCounter].TextMessage != null)
            {
                mainTextMessageList[telephoneNumberCounter].TextMessage = mainTextMessageList[telephoneNumberCounter].TextMessage + "\r\nGenerated: " + date.ToShortTimeString() + " " + date.ToShortDateString();
            }


            telephoneNumberCounter++;
        }


        //Turn array into large string
        string fullTextMessageString = "";

        for (int y = 0; y < telephoneNumberCounter; y++)
        {
            if (mainTextMessageList[y].TextMessage != null)
            {
                fullTextMessageString = fullTextMessageString + mainTextMessageList[y].telephoneNumber + "|" + mainTextMessageList[y].TextMessage + "#";
            }
        }

        //fullTextMessageString= fullTextMessageString.Substring(0,fullTextMessageString.Length-1);

        return fullTextMessageString;
        
    }  //end GenerateAdHoc

    private void RetrieveAllowedEnvs(int textType, ref allowedEnvironments[] environments)
    {
        string mySelectQuery = "";
        int counter = 0;
        DateTime date = DateTime.Now;

        //check if its weekend or weekday
        Boolean weekend = false;

        if ((date.DayOfWeek.ToString() == "Saturday") || (date.DayOfWeek.ToString() == "Sunday"))
        {
            weekend = true;
        }
        else
        {
            weekend = false;
        }
            

        if (textType == 0)
        {
            if (weekend = true)
            {

                mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where SummaryAlertText = 1 and WeekendAlert=1";
            }
            else
            {
                mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where SummaryAlertText = 1 and DayAlertText=1";
            }
        }
        else if (textType == 1)
        {

            //check if it is day or night, typical day starts at 06:00 and ends at 22:00
                                   
            if ((date.Hour >= 22 && date.Hour <= 24) || (date.Hour >= 00 && date.Hour <= 5))
            {
                if (weekend = true)
                {
                    mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where NightAlertText = 1 and WeekendAlert=1"; 
                }
                else 
                {
                    mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where NightAlertText = 1 and DayAlertText=1";
                }
            }
            else
            {
                if (weekend = true)
                {
                    mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where (time(weekdayAlertTime) <= time(now())) and weekendAlert=1";
                }
                else
                {
                    mySelectQuery = "Select PhoneNumber,EnvironmentsAllowed from AutoH where DayAlertText = 1 and (time(weekdayAlertTime) <= time(now()))";
                }
            }            
        }

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        MySqlDataReader myReader;

        //if the db is unavailable then it's going to crash here
        try
        {
            myConnection.Open();
            myReader = myCommand.ExecuteReader();

            while (myReader.Read())
            {
                //myStatusMessage += myReader.GetString(0) + "\r\n";   //email

                //0	PhoneNumber,EnvironmentsAllowed
                //1	EnvironmentsAllowed

                environments[counter].telephoneNumber = myReader.GetString(0);
                environments[counter].environments = myReader.GetString(1);
                counter++;
            }

            myReader.Close();
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR whilst reading RetrieveAllowedEnvs" + ex.Message);
        }
        finally
        {

            myConnection.Close();
        }


    }  //end RetrieveAllowedEnvs

    private bool CheckApplicationHistory(int type_id, string environment, ref string failureTime, ref int entry_id)
    {
        string mySelectQuery = "";
       // int counter = 0;        

        //select DATE_FORMAT(test_datetime,"%T %d/%m"),app_availability, entry_id from ApplicationAvailabilityHistory 
        //where entry_id = (select max(entry_id) from ApplicationAvailabilityHistory where application_id=0 AND environment='NIS5');

        mySelectQuery = "select DATE_FORMAT(test_datetime,\"%k:%i %d/%m\"),app_availability, entry_id from ApplicationAvailabilityHistory ";
        mySelectQuery = mySelectQuery + "where entry_id = (select max(entry_id) from ApplicationAvailabilityHistory where application_id=" + type_id + " AND environment='" + environment + "')";
        
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        MySqlDataReader myReader;

        //if the db is unavailable then it's going to crash here
        try
        {
            myConnection.Open();
            myReader = myCommand.ExecuteReader();


            while (myReader.Read())
            {
                //set failuretime in paramenter passed by ref
                failureTime = myReader.GetString(0);
                entry_id = Convert.ToInt32(myReader.GetString(2));

                if (myReader.GetString(1) == "1")
                {
                    //The app has passed
                    return true;
                }
                else
                {
                    //The app has failed
                    return false;
                }

            }

            myReader.Close();
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR whilst running CheckApplicationHistory.stack trace :" + ex.Message);
        }

        finally
        {

            myConnection.Close();
        }

        return true;

    }  //end CheckApplicationHistory

    private bool CheckEnvironmentBlock(string environment)
    {
        string mySelectQuery = "";
        DateTime date = DateTime.Now;
        // int counter = 0;        

        //select environment from TextMessageEnvironmentBlock where time_to_stop_messages<= '' AND time_to_resume_messages>='';
        mySelectQuery = "select environment from TextMessageEnvironmentBlock where "
                        + "time_to_stop_messages<= '" + date.ToString("yyyy/MM/dd HH:mm:ss") + "' AND time_to_resume_messages>='" + date.ToString("yyyy/MM/dd HH:mm:ss") + "' "
                        + " AND environment = '" + environment +"';";
        
        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
        MySqlDataReader myReader;

        //if the db is unavailable then it's going to crash here
        try
        {
            myConnection.Open();
            myReader = myCommand.ExecuteReader();

            try
            {
                while (myReader.Read())
                {
                    //environment has been found to be blocked return true
                    return true;
                }
            }

            catch (Exception ex)
            {
                myAlertSMS.MyEventLog("DB ERROR whilst reading telephone data.stack trace :" + ex.Message);
            }

            finally
            {
                myReader.Close();
                myConnection.Close();
            }
        }
        catch (Exception ex)
        {
            myAlertSMS.MyEventLog("DB ERROR. stack trace:" + ex.Message);

        }

        return false;

    }  //end CheckEnvironmentBlock

    private bool UpdateApplicationHistory(int type_id, string environment, int entry_id, int availability)
    {
        string myUpdateQuery = "";
        DateTime date = DateTime.Now;

        //insert into ApplicationAvailabilityHistory_TEST (application_id, app_availability, text_message_id, test_datetime, environment, corresponding_entry_id) values ("
        myUpdateQuery = "insert into ApplicationAvailabilityHistory(application_id, app_availability, text_message_id, test_datetime, environment, corresponding_entry_id, outage_status, outage_comments, outage_declared, outage_rca) values (";
        myUpdateQuery = myUpdateQuery + type_id.ToString() + "," + availability.ToString() +",0,\"" + date.ToString("yyyy/MM/dd HH:mm:ss") + "\",\"" + environment
            + "\"," + entry_id.ToString() + ",0,\"\",0,\"\");";

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(myUpdateQuery, myConnection);

        try
        {
            myConnection.Open();
            myCommand.ExecuteNonQuery();
            myCommand.Connection.Close();
        }
        catch (Exception ex)
        {
            //log the problem. if the insert doesn't work it will select again and keep trying
            myAlertSMS.MyEventLog("DB ERROR. stack trace." + ex.Message);
            return false;
        }
        finally
        {
            myConnection.Close();
            myCommand.Connection.Close();
        }
        return true;

    }  //end UpdateApplicationHistory

    private bool UpdateAutoHMessageSummary(string phoneNumber, DateTime lastSummaryMessageSent)
    {
        string myUpdateQuery = "";
        DateTime date = DateTime.Now;

        //iupdate AutoH setsummaryMessageLastSent ='something' where PhoneNumber=something
        myUpdateQuery = "update AutoH set summaryMessageLastSent ='" + lastSummaryMessageSent.ToString("yyyy/MM/dd HH:mm:ss") 
                        + "' where PhoneNumber=" + phoneNumber +";";

        MySqlConnection myConnection = new MySqlConnection(connectionString);
        MySqlCommand myCommand = new MySqlCommand(myUpdateQuery, myConnection);

        try
        {
            myConnection.Open();
            myCommand.ExecuteNonQuery();
            myCommand.Connection.Close();
        }
        catch (Exception ex )
        {
            //log the problem. if the insert doesn't work it will select again and keep trying
            myAlertSMS.MyEventLog("DB ERROR. Stack Trace :" + ex.Message);
            return false;
        }
        finally
        {
            myConnection.Close();
            myCommand.Connection.Close();
        }
        return true;

    }  //end UpdateAutoHMessageSummary

}
