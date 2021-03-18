using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;
using System.IO;

using MySql.Data.MySqlClient;
using ASmsCtrl;
using System.Net;

using System.Configuration;
using System.Web;

namespace AlertSMS
{
    public partial class Service1 : ServiceBase
    {
        Worker workerObject;
        Thread workerThread;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.

            // Create the thread object. This does not start the thread.
            workerObject = new Worker();
            workerThread = new Thread(workerObject.DoWork);

            // Start the worker thread.
            workerThread.Start();
            EventLog.WriteEntry("AlertSMS", "main thread: Starting worker thread...");

            // Loop until worker thread activates.
            while (!workerThread.IsAlive) ;

            // Put the main thread to sleep for 1 millisecond to
            // allow the worker thread to do some work:
            //Thread.Sleep(1);

        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.

            // Request that the worker thread stop itself:
            workerObject.RequestStop();

            // Use the Join method to block the current thread 
            // until the object's thread terminates.
            workerThread.Join();
            EventLog.WriteEntry("AlertSMS", "main thread: Worker thread has terminated.");
        }

        public class Worker
        {
            // This method will be called when the thread is started.
            public void DoWork()
            {
                bool sleepyTime = false;
                bool startUpDelay = true; //called when started up in order to attach when debugging

                int counter = 0;
                int counterToggle = 10;

                while (!_shouldStop)
                {
                    //sleep for a time at start up to allow remote debugging
                    if (startUpDelay == true)
                    {
                        startUpDelay = false;
                        EventLog.WriteEntry("AlertSMS", "Started DoWork. One time sleeping for 30 sec. Attach to process NOW for debugging.");
                        Thread.Sleep(30000); // 30 sec
                        EventLog.WriteEntry("AlertSMS", "Finished start up sleep.");
                    }


                    if (sleepyTime)
                        Thread.Sleep(10000); // 10 sec
                    else
                        sleepyTime = true;

                    //######Do stuff here######
                    //To convert find "Console." replace with "//#!#Console." 
                    //To convert find "//#?#" /**/ 

                    int smsOutArrSize = 0;
                    string[,] smsOutMainArr;

                    DbClass myDbClass = new DbClass();
                    GsmClass myGsm = new GsmClass();
                    AlertSMS myAlertSMS = new AlertSMS();
                    Logger myLog = new Logger();


                    myDbClass.ReadSmsOutTable("SELECT SmsOutId, number, message, timestamp, sent FROM SmsOut WHERE sent = -1 AND timestamp > (now() - INTERVAL 15 MINUTE) ORDER BY SmsOutId", out smsOutMainArr, ref smsOutArrSize);

                    if (smsOutArrSize > 0)
                    {
                        if (!myGsm.GsmSendMessages(ref smsOutMainArr, smsOutArrSize))
                        {
                            //The GSM model has failed or a message sending has failed. Attempt to send via Click-a-Tell
                            Click_A_TellGsmClass clkatellObj = new Click_A_TellGsmClass();
                            clkatellObj.ClickASendMessages(ref smsOutMainArr, smsOutArrSize);
                        }
                    }

                    //check GSM modem for any received messages and populate the db smsin table
                    if (!myGsm.GsmRetrieveMessages())
                    {
                        //Failed to retrieve messages - need to log this
                        myLog.WriteLog("Unable to connect to \"Teltonika ModemUSB G10\" modem");
                        continue;
                    }

                    //check db smsin table for any received messages or unprocessed messages
                    int smsInArrSize = 0;
                    string[,] smsInMainArr;
                    myDbClass.ReadSmsInTable("SELECT SmsInId, number, message, timestamp, status FROM smsin WHERE status = -1 AND timestamp > (now() - INTERVAL 15 MINUTE) ORDER BY SmsInId", out smsInMainArr, ref smsInArrSize);

                    /*
                     * nis5 sms off
                     * nis5 sms off 5       //off for 5 hrs
                     * nis5 sms on
                     * nis2 tests off
                     * nis2 tests off 2     //off for 2hrs
                     * nis2 tests on
                     */

                    if (smsInArrSize > 0)
                    {
                        bool longSleep = false;

                        for (int i = 0; i < smsInArrSize; i++)
                        {
                            //extract phone number and message
                            string phoneNumber = smsInMainArr[i, 1];
                            string recMessage = smsInMainArr[i, 2].TrimStart().TrimEnd().ToUpper();

                            //firstly check for misc message
                            string miscMessage;
                            if (myAlertSMS.MiscCheck(recMessage, out miscMessage))
                            {
                                //confirmed as update, send a confirmation mail
                                myDbClass.GeneralInsertUpdate("INSERT INTO SmsOut (number, message, timestamp, sent) values ('" + phoneNumber
                                                            + "', '" + miscMessage + "', now(), -1)");

                                //update smsin message as processed
                                string mySmsInId = "UPDATE smsin SET status = 1 WHERE SmsInId = '";
                                mySmsInId += smsInMainArr[i, 0];
                                mySmsInId += "'";
                                myDbClass.GeneralInsertUpdate(mySmsInId);
                                continue;
                            }


                            //post the above to the ADC website
                            string adcResponse, adcStatus;
                            bool statusReq = true;
                            if (myAlertSMS.SubmitMessageToAdc(recMessage, phoneNumber, statusReq, out adcResponse, out adcStatus) == false)
                            {
                                myLog.WriteLog("Unable to connect to ADC Website:" + smsInMainArr[i, 0] + " " + adcResponse);
                                continue;
                            }

                            string responseTxtBxLB = "<input name=\"responseTxtBx\" type=\"text\" value=\"";
                            string responseTxtBxRB = "\" id=\"responseTxtBx\" />";
                            int rlb = adcResponse.IndexOf(responseTxtBxLB) + responseTxtBxLB.Length;
                            int rrb = adcResponse.IndexOf(responseTxtBxRB, rlb);

                            //this will hold any message response response
                            string adcRsp = adcResponse.Substring(rlb, rrb - rlb);

                            if (adcStatus == "1") //successful mystatus, day off/on/summary, env block
                            {

                                //successful process
                                string mySmsInId = "UPDATE smsin SET status = 1 WHERE SmsInId = ";
                                mySmsInId += smsInMainArr[i, 0];
                                myLog.WriteLog("Update smsin with:" + smsInMainArr[i, 0]);
                                myDbClass.GeneralInsertUpdate(mySmsInId);

                                //put the received message into the smsout table for processing
                                myDbClass.GeneralInsertUpdate("INSERT INTO SmsOut (number, message, timestamp, sent) values ('" + phoneNumber
                                                            + "', '" + adcRsp + "', now(), -1)");

                                continue;
                            }


                            if (adcStatus == "2") //unknown phone number, do nothing
                            {
                                //2 - unknown phone number - will go to dead letter in ADC
                                string mySmsInId = "UPDATE smsin SET status = 2 WHERE SmsInId = ";
                                mySmsInId += smsInMainArr[i, 0];
                                myLog.WriteLog("Unknown Phone number. Update smsin with:" + mySmsInId);
                                myDbClass.GeneralInsertUpdate(mySmsInId);
                                continue;
                            }


                            if (adcStatus == "3") //unknown message content, do nothing
                            {                            
                                //3 - invalid content - will go to dead letter in ADC
                                string mySmsInId = "UPDATE smsin SET status = 3 WHERE SmsInId = ";
                                mySmsInId += smsInMainArr[i, 0];
                                myLog.WriteLog("Invalid content. Update smsin with:" + mySmsInId);
                                myDbClass.GeneralInsertUpdate(mySmsInId);

                                //respond to the user with a list of commands?
                                continue;
                            }


                            if (adcStatus == "4")
                            {
                                //4 - request for status - to be picked up by loadrunner
                                string mySmsInId = "UPDATE smsin SET status = 4 WHERE SmsInId = ";
                                mySmsInId += smsInMainArr[i, 0];
                                myLog.WriteLog("Status request sent to ADC. Update smsin with:" + mySmsInId);
                                myDbClass.GeneralInsertUpdate(mySmsInId);


                                //this is a status message so extract the contents of the textAlertOutbound
                                string textAlertOutboundLB = "<input name=\"textAlertOutbound\" type=\"text\" value=\"";
                                string textAlertOutboundRB = "\" id=\"textAlertOutbound\" />";
                                int talb = adcResponse.IndexOf(textAlertOutboundLB);

                                if (talb < 0)
                                {
                                    myLog.WriteLog("Cannot extract message from testAlertOutbound. talb");
                                    continue;
                                }

                                talb = talb + textAlertOutboundLB.Length;

                                int tarb = adcResponse.IndexOf(textAlertOutboundRB, talb);

                                if (tarb < 0)
                                {
                                    myLog.WriteLog("Cannot extract message from testAlertOutbound. tarb");
                                    continue;
                                }

                                string adcOutBoundMessage = adcResponse.Substring(talb, tarb - talb);

                                //string adcOutBoundMessage = "447595288924|NIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447795987541|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447946492523|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447702956190|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447918029862|\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447710039308|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#";

                                string targetNumber = "";
                                string outboundMessage = "";

                                string[] spiltByHashArr = adcOutBoundMessage.Split('#');

                                foreach (string message in spiltByHashArr)
                                {
                                    //now split by number and message
                                    string[] spiltByPipeArr = message.Split('|');

                                    targetNumber = spiltByPipeArr[0];
                                    outboundMessage = spiltByPipeArr[1];
                                    break;
                                }

                                //now update the smsout table
                                myDbClass.GeneralInsertUpdate("INSERT INTO SmsOut (number, message, timestamp, sent) values ('" + targetNumber
                                                            + "', '" + outboundMessage + "', now(), -1)");

                                continue;
                            }


                            //if we get to this point then we have a problem with this particular message
                            if (adcStatus == "999")
                            {
                                myLog.WriteLog("DB Error thrown in ADC.");
                                continue;
                            }

                            longSleep = true;
                            myLog.WriteLog("ERROR. Unable to process message:" + smsInMainArr[i, 0]);


                        } //end for

                        if (longSleep)
                        {
                            longSleep = false;
                            myLog.WriteLog("Sleeping for 30 secs");
                            Thread.Sleep(30000); // 30 sec
                        }


                    } //end if smsInArrSize

                    if (counter == counterToggle)
                    {
                        counter = 0;


                        //Post to the ADC and check if there are any alerts or am messages to be sent out
                        string adcResponse2, adcStatusAlert;
                        bool statusReq2 = false; //TODO - is this required?
                        if (myAlertSMS.SubmitMessageToAdc("", "", statusReq2, out adcResponse2, out adcStatusAlert) == false)
                        {
                            myLog.WriteLog("Unable to connect to ADC Website to check for alerts or am messages" + " " + adcResponse2);
                            continue;
                        }

                        //status should be 5
                        if (adcStatusAlert != "5") //unknown message content, do nothing
                        {
                            myLog.WriteLog("ADC not responded to alert check with status of 5");

                            if (adcStatusAlert != "999")
                                myLog.WriteLog("ADC responded with status of 999");

                            continue;
                        }


                        //extract the response status and 
                        string textAlertOutboundLB2 = "<input name=\"textAlertOutbound\" type=\"text\" value=\"";
                        string textAlertOutboundRB2 = "\" id=\"textAlertOutbound\" />";
                        int talb2 = adcResponse2.IndexOf(textAlertOutboundLB2);

                        if (talb2 < 0)
                            continue;

                        talb2 = talb2 + textAlertOutboundLB2.Length;

                        int tarb2 = adcResponse2.IndexOf(textAlertOutboundRB2, talb2);

                        if (tarb2 < 0)
                            continue;

                        string adcOutBoundMessage2 = adcResponse2.Substring(talb2, tarb2 - talb2);

                        //string adcOutBoundMessage = "447595288924|NIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447795987541|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447946492523|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447702956190|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447918029862|\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#447710039308|\r\nNIS1\r\n-------\r\nACFSDS F (was P)\r\n\r\nTS2\r\n-------\r\nERS F (was P)\r\n\r\nVNIS\r\n-------\r\nACFSDS F (was P)\r\n\r\nGenerated: 15:09 28/01/2010|#";

                        string targetNumber2 = "";
                        string outboundMessage2 = "";

                        string[] spiltByHashArr2 = adcOutBoundMessage2.Split('#');

                        foreach (string message in spiltByHashArr2)
                        {
                            //now split by number and message
                            string[] spiltByPipeArr = message.Split('|');

                            targetNumber2 = spiltByPipeArr[0];
                            outboundMessage2 = spiltByPipeArr[1];

                            //now update the smsout table
                            myDbClass.GeneralInsertUpdate("INSERT INTO SmsOut (number, message, timestamp, sent) values ('" + targetNumber2
                                                        + "', '" + outboundMessage2 + "', now(), -1)");

                        }

                    }
                    else
                        counter++;



                } //end while

                EventLog.WriteEntry("AlertSMS", "worker thread: terminating gracefully.");

            }

            public void RequestStop()
            {
                _shouldStop = true;
            }
            // Volatile is used as hint to the compiler that this data member will be accessed by multiple threads.
            private volatile bool _shouldStop;
        }

    }

    //###### Add Classes here ######
    class AlertSMS
    {

        public bool SubmitMessageToAdc(string recMessage, string phoneNumber, bool statusReqest, out string objResponseContent, out string adcStatus)
        {
            //string targetUrlSSL = "https://eitnic1.npfit.nhs.uk/AdcSMS/PostSMS.aspx";
            string targetUrlSSL = "http://10.206.131.75/AdcSMS/PostSMS.aspx";

            objResponseContent = "";
            adcStatus = "";
            

            if (statusReqest == true && phoneNumber.Length == 0)
                return false;

            WebResponse objResponse;
            WebRequest objRequest = System.Net.HttpWebRequest.Create(targetUrlSSL);

            try
            {
                objResponse = objRequest.GetResponse();
   
                using (StreamReader sr = new StreamReader(objResponse.GetResponseStream()))
                {
                    objResponseContent = sr.ReadToEnd();
                    sr.Close();
                }

                objResponse.Close();
            }
            catch (Exception e)
            {
                objResponseContent = e.ToString();
                return false;
            }

            //extract the view state
            string vsStr = "id=\"__VIEWSTATE\" value=\"";
            int vsLB = objResponseContent.IndexOf(vsStr);
            int vsRB = objResponseContent.IndexOf("\" />", vsLB);

            string viewState = objResponseContent.Substring(vsLB + vsStr.Length, vsRB - vsLB - vsStr.Length);

            viewState = HttpUtility.UrlEncode(viewState);

            //extract validation guid
            string evStr = "id=\"__EVENTVALIDATION\" value=\"";
            int evLB = objResponseContent.IndexOf(evStr);
            int evRB = objResponseContent.IndexOf("\" />", evLB);

            string eval = objResponseContent.Substring(evLB + evStr.Length, evRB - evLB - evStr.Length);
            eval = HttpUtility.UrlEncode(eval);

            ASCIIEncoding encoding = new ASCIIEncoding();

            string postData = "__VIEWSTATE=" + viewState
                            + "&contentTxtBx=" + recMessage
                            + "&phoneNumberTxtBx=" + phoneNumber
                            + "&statusMessageTxtBx=preclick&__EVENTVALIDATION="
                            + eval + "&postSmsButt=postSmsButt";


            byte[] data = encoding.GetBytes(postData);

            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(targetUrlSSL);
            myRequest.Method = "POST";
            myRequest.ContentType = "application/x-www-form-urlencoded";
            myRequest.ContentLength = data.Length;
            Stream newStream = myRequest.GetRequestStream();

            // Send the data.   
            newStream.Write(data, 0, data.Length);
            newStream.Close();

            string content = "";
            // Get response   
            try
            {
                HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
                StreamReader reader = new StreamReader(myResponse.GetResponseStream(), Encoding.Default);
                content = reader.ReadToEnd();
            }
            catch (Exception exep)
            {
                objResponseContent = exep.ToString();
                return false;
            }

            objResponseContent = content;

            //extract the response status and 
            string statusMessageTxtBxLB = "<input name=\"statusMessageTxtBx\" type=\"text\" value=\"";
            string statusMessageTxtBxRB = "\" id=\"statusMessageTxtBx\" />";

            //int slb = content.IndexOf(statusMessageTxtBxLB) + statusMessageTxtBxLB.Length;
            //int srb = content.IndexOf(statusMessageTxtBxRB, slb);

            int slb = content.IndexOf(statusMessageTxtBxLB);
            if (slb < 0)
            {
                objResponseContent += " Unable to find statusMessageTxtBxLB";
                return false;
            }

            slb = slb + statusMessageTxtBxLB.Length;


            int srb = content.IndexOf(statusMessageTxtBxRB, slb);

            if (srb < 0)
            {
                objResponseContent += " Unable to find statusMessageTxtBxRB";
                return false;
            }

            //this will hold the int status response
            adcStatus = content.Substring(slb, srb - slb);

            return true;
        }

        public bool ChangeSmsAlerts(string recMessage, ref string autohId)
        {
            DbClass myDbClass = new DbClass();

            //attempt to split the message by carriage return
            string mySeparated = recMessage.Replace("\r\n", "|");
            string[] split = null;
            split = mySeparated.Split('|');
            string buildChangeScript = "UPDATE autoh SET ";

            bool matchFlag = false; //check if match found, otherwise set to dead letter
            bool dayFlag = false;
            bool nightFlag = false;
            bool summaryFlag = false;

            foreach (string splitString in split)
            {
                if (splitString.StartsWith("DAY"))
                {
                    //once per message
                    if (!dayFlag)
                        dayFlag = true;
                    else
                        continue;

                    //trim whitespace 
                    string buff = splitString.Substring(4);
                    buff = buff.TrimStart().TrimEnd();
                    string changeInt = "0"; //give a value
                    if (buff == "ON")
                        changeInt = "1";
                    else if (buff == "OFF")
                        changeInt = "0";
                    else //can't read what's in message
                        continue;

                    buildChangeScript += "DayAlertText = '" + changeInt + "', ";

                    matchFlag = true;
                }

                if (splitString.StartsWith("NIGHT"))
                {
                    //once per message
                    if (!nightFlag)
                        nightFlag = true;
                    else
                        continue;

                    //trim whitespace 
                    string buff = splitString.Substring(5);
                    buff = buff.TrimStart().TrimEnd();
                    string changeInt = "0";
                    if (buff == "ON")
                        changeInt = "1";
                    else if (buff == "OFF")
                        changeInt = "0";
                    else //can't read what's in message
                        continue;

                    buildChangeScript += "NightAlertText = '" + changeInt + "', ";

                    matchFlag = true;
                }

                if (splitString.StartsWith("SUMMARY"))
                {
                    //once per message
                    if (!summaryFlag)
                        summaryFlag = true;
                    else
                        continue;

                    //trim whitespace 
                    string buff = splitString.Substring(7);
                    buff = buff.TrimStart().TrimEnd();
                    string changeInt = "0";
                    if (buff == "ON")
                        changeInt = "1";
                    else if (buff == "OFF")
                        changeInt = "0";
                    else //can't read what's in message
                        continue;

                    buildChangeScript += "SummaryAlertText = '" + changeInt + "', ";

                    matchFlag = true;
                }

            } //end foreach

            if (matchFlag)
            {
                //make the change
                if (buildChangeScript.EndsWith(", "))
                {
                    buildChangeScript = buildChangeScript.Substring(0, (buildChangeScript.Length - 2));
                    buildChangeScript += " ";
                }

                buildChangeScript += "WHERE id = " + autohId;
                myDbClass.GeneralInsertUpdate(buildChangeScript);
            }

            return matchFlag;

        }

        public bool ChangeEnvBlock(string recMessage, ref string autohId, out string respMessage)
        {
            DbClass myDbClass = new DbClass();

            respMessage = "";

            //attempt to split the message by carriage return
            string mySeparated = recMessage.Replace("\r\n", "|");
            string[] split = null;
            split = mySeparated.Split('|');
            string buildChangeScript = "";
            string envBlockDeleteScript = "";

            bool matchFlag = false; //check if match found, otherwise set to dead letter
            bool smsOnFlag = false;

            foreach (string splitString in split)
            {
                if (splitString.StartsWith("NIS") || splitString.StartsWith("VNIS") || splitString.StartsWith("VPOC") )
                {
                    //if matched already ignore further lines
                    if (matchFlag)
                        continue;

                    //if a nis numbered env then check the number is valid //envNum = Convert.ToInt32(env);
                    if (splitString.StartsWith("NIS"))
                    {
                        //string buff = envNum = splitString.Substring(3, 1);
                        int envNum = Convert.ToInt32(splitString.Substring(3, 1));
                        if (envNum < 1 || envNum > 9)
                            continue;
                    }

                    //validate it if not vnis
                    int bspace = 0;

                    bspace = splitString.IndexOf(" ");

                    if (bspace <=0)
                        continue;

                    string environment = splitString.Substring(0, bspace);

                    string remainingMessage = splitString.Substring(bspace).TrimStart();

                    if (remainingMessage.StartsWith("TESTS"))
                    {
                        buildChangeScript += "UPDATE runningtests SET running = ";
                        remainingMessage = remainingMessage.Substring(5).TrimStart();

                        if (remainingMessage.StartsWith("ON"))
                        {
                            respMessage = environment + " tests set to ON";
                            buildChangeScript += "1 WHERE environment = '";
                        }

                        else if (remainingMessage.StartsWith("OFF"))
                        {
                            respMessage = environment + " tests set to OFF";
                            buildChangeScript += "0 WHERE environment = '";
                        }

                        else
                            continue;

                        buildChangeScript += environment + "'";
                        matchFlag = true;

                    }

                    //INSERT INTO autoharness.textmessageenvironmentblock (environment, time_to_stop_messages, time_to_resume_messages) VALUES ( 'NIS4', now(), now() + interval 12 hour );
                    else if (remainingMessage.StartsWith("SMS"))
                    {
                        envBlockDeleteScript = "DELETE FROM autoharness.textmessageenvironmentblock WHERE environment = '" + environment + "'";
                        remainingMessage = remainingMessage.Substring(3).TrimStart();

                        if (remainingMessage.StartsWith("OFF"))
                        {
                            smsOnFlag = true;
                            buildChangeScript += "INSERT INTO autoharness.textmessageenvironmentblock (environment, time_to_stop_messages, time_to_resume_messages) VALUES ( '";
                            buildChangeScript += environment + "', now(), now() + interval ";

                            //check if there is a hours interval
                            remainingMessage = remainingMessage.Substring(3).TrimStart().TrimEnd();

                            if (remainingMessage.Length > 0)
                            {
                                int hrs = 0;

                                try
                                {
                                    hrs = Convert.ToInt32(remainingMessage);
                                }
                                catch
                                {
                                    hrs = 500;
                                }

                                if (hrs < 1 || hrs > 10000)
                                    hrs = 500;

                                buildChangeScript += Convert.ToString(hrs);

                                respMessage = environment + " SMS messages blocked for " + Convert.ToString(hrs);
                                if (hrs == 1)
                                     respMessage += " hour starting now";
                                else
                                     respMessage += " hours starting now";
                            }
                            else
                            {
                                buildChangeScript += "500";
                                respMessage = environment + " SMS message block inserted for 500 hours starting now";
                            }

                            buildChangeScript += " hour )";
                            
                            matchFlag = true;
                        }

                        else if (remainingMessage.StartsWith("ON"))
                        {
                            buildChangeScript = envBlockDeleteScript;
                            respMessage = environment + " SMS messages are unblocked";
                            matchFlag = true;
                        }
                        else
                            continue;
                    }

                    else //no match
                        continue;



                } //end if


            } //end foreach

            if (smsOnFlag)
                myDbClass.GeneralInsertUpdate(envBlockDeleteScript);

            if (matchFlag)
            {
                myDbClass.GeneralInsertUpdate(buildChangeScript);
            }

            return matchFlag;

        }

        public bool MiscCheck(string recMessage, out string textBody)
        {
            bool matchFlag = false;
            textBody = "";

            if (recMessage != "CLYDEWEATHER")
            {
                matchFlag = false;
                return matchFlag;
            }


            string url = "http://www.metoffice.gov.uk/weather/marine/inshore_forecast.html?area=14&type=All&x=10&y=10";
            string strResult = "";

            WebResponse objResponse;
            WebRequest objRequest = System.Net.HttpWebRequest.Create(url);


            objResponse = objRequest.GetResponse();

            using (StreamReader sr = new StreamReader(objResponse.GetResponseStream()))
            {
                strResult = sr.ReadToEnd();
                // Close and clean up the StreamReader
                sr.Close();
            }

            textBody = "Mull Galloway 2 Mull Kintyre inc Clyde\r\n";

            //if we have a strong wind warning then add it
            string strongWindWarning = "<strong>The Mull of Galloway to Mull of Kintyre including Firth of Clyde,</strong>";
            int sswLB = strResult.IndexOf(strongWindWarning);

            if (sswLB > 0)
            {
                textBody += "Strong Wind Warn:";
                int sswRB = strResult.IndexOf("</p>", sswLB);
                string strongWW = strResult.Substring((sswLB + strongWindWarning.Length), (sswRB - sswLB - strongWindWarning.Length)).TrimStart().TrimEnd();

                //format out the html
                strongWW = strongWW.Replace("<br/>", "");
                strongWW = strongWW.Replace("&nbsp;", "");
                strongWW = strongWW.Replace("    ", "");

                textBody += strongWW;

                matchFlag = true;
            }

            //extract the body of the forecast
            string forcast = "<strong>The Mull of Galloway to Mull of Kintyre including the Firth of Clyde and the North Channel</strong>";
            int forecastLB = strResult.IndexOf(forcast);

            if (forecastLB > 0)
            {
                textBody += "\r\nInshore Forcast (24hr)";
                int forecastRB = strResult.IndexOf("</p>", forecastLB);
                string forecastBody = strResult.Substring((forecastLB + forcast.Length), (forecastRB - forecastLB - forcast.Length)).TrimStart().TrimEnd();

                //format out the html
                forecastBody = forecastBody.Replace("<br/>", "");
                forecastBody = forecastBody.Replace("<br>", "");
                forecastBody = forecastBody.Replace("&nbsp;", "");
                forecastBody = forecastBody.Replace("&nbsp", "");
                forecastBody = forecastBody.Replace("    ", "");
                forecastBody = forecastBody.Replace("<strong>", "");
                forecastBody = forecastBody.Replace("</strong>", "");
                forecastBody = forecastBody.Replace("<!-- Outlook for the following 24 hours: -->\r\n\t\t\t\t  ", "");

                string mySeparated = forecastBody.Replace("\r\n\t\t\t\t  ", "|");

                string[] myForcastArr = mySeparated.Split('|');

                myForcastArr[6] = "\r\n";
                myForcastArr[7] = "Outlook(24-48hr)";

                for (int i = 2; i < myForcastArr.Length - 1; i++)
                {
                    if (i == 2 || i == 8)
                        textBody += "\r\n" + "Wind:";
                    if (i == 3 || i == 9)
                        textBody += "\r\n" + "Sea State:";
                    if (i == 4 || i == 10)
                        textBody += "\r\n" + "Weather:";
                    if (i == 5 || i == 11)
                        textBody += "\r\n" + "Vis:";

                    textBody += myForcastArr[i];
                }

                textBody += "";

                matchFlag = true;
            }

            return matchFlag;
        }

        //public void MyEventLog(string logString)
        //{
        //    EventLog.WriteEntry("AlertSMS", logString);
        //}

        //bool amUpdateSent = true; //default true

        //DateTime dNow = DateTime.Now; //DateTime.Parse("28 Jan 2010 06:31:00");
        //DateTime nextUpdateDay = DateTime.Today.AddDays(1); //("29 Jan 2010 00:00:00")
        //DateTime amUpdateOutTime = DateTime.Parse("01 Jan 1900 06:30:00"); //default am time

        //public bool SendAmUpdate()
        //{
        //    bool functionReturn = false;

        //    // if it's midnight and we've just gone into a new day
        //    // then set the amUpdateSent value to false
        //    if ((dNow >= nextUpdateDay) && amUpdateSent == true)
        //    {
        //        //its a new day! so amUpdateSet = false
        //        amUpdateSent = false;

        //        //the next day will now need updating
        //        nextUpdateDay = nextUpdateDay.AddDays(1);//DateTime.Today.AddDays(1);
        //    }

        //    // if the amUpdateSent is false and the time is greater than or = to 6:30am
        //    // then send the amUpdate and toggle the amUpdateSent value
        //    // to true
        //    if ((amUpdateSent == false) && (dNow.Hour >= amUpdateOutTime.Hour) && (dNow.Minute >= amUpdateOutTime.Minute))
        //    {
        //        amUpdateSent = true;

        //        /* 
        //         * SEND THE UPDATE CODE HERE
        //         */
        //        functionReturn = true;
        //    }

        //    return functionReturn;
        //}


    }

    class DbClass
    {
        string connectionString = "server=10.206.131.75;database=AutoHarness;user id=eit;password=eit;persist security info=True";

        //AlertSMS myAlertSMS = new AlertSMS();
        Logger myLog = new Logger();

        public void ReadSmsOutTable(string mySelectQuery, out string[,] smsOutArr, ref int arrSize)
        {
            int maxBatchSize = 20;
            smsOutArr = new string[maxBatchSize, 5]; //rows, elements in row

            MySqlConnection myConnection = new MySqlConnection(connectionString);
            //myConnection.Open();

            MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
            MySqlDataReader myReader;
            //if the db is unavailable then it's going to crash here
            try
            {
                myConnection.Open();
                myReader = myCommand.ExecuteReader();
            }
            catch (Exception e)
            {
                /**/ Thread.Sleep(120000); // 2 min
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                /**/
                return;
            }

            int i = 0;

            try
            {

                while (myReader.Read())
                {
                    if (i == 20)
                    {
                        break;
                    }

                    smsOutArr[i, 0] = myReader.GetString(0);   //SmsOutId
                    string temp = myReader.GetString(1);

                    if (temp.Substring(0, 1) == "+")
                    {
                        //string checkForPlus = temp[1];
                        smsOutArr[i, 1] = temp.Substring(1, temp.Length - 1);
                    }
                    else
                        smsOutArr[i, 1] = myReader.GetString(1);   //number

                    smsOutArr[i, 2] = myReader.GetString(2);   //Message
                    smsOutArr[i, 3] = myReader.GetString(3);   //timestamp
                    smsOutArr[i, 4] += myReader.GetString(4);   //sent or not -1=not sent; 0=fail; 1=success
                    i++;
                }

                arrSize = i;

            }

            catch (Exception e)
            {
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                Thread.Sleep(120000); // 2 min
            }

            finally
            {
                myReader.Close();
                myConnection.Close();
            }

        }  //end  ReadSmsOutTable

        public void ReadSmsInTable(string mySelectQuery, out string[,] smsInArr, ref int arrSize)
        {
            int maxBatchSize = 20;
            smsInArr = new string[maxBatchSize, 4]; //rows, elements in row

            MySqlConnection myConnection = new MySqlConnection(connectionString);
            MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
            MySqlDataReader myReader;

            //if the db is unavailable then it's going to crash here
            try
            {
                myConnection.Open();
                myReader = myCommand.ExecuteReader();
            }
            catch (Exception e)
            {
                /**/ Thread.Sleep(120000); // 2 min
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                /**/
                return;
            }

            int i = 0;

            try
            {
                while (myReader.Read())
                {
                    if (i == 20) // limit the amount of messages to a 20 batch
                    {
                        break;
                    }

                    smsInArr[i, 0] = myReader.GetString(0);   //SmsInId
                    smsInArr[i, 1] = myReader.GetString(1);   //number
                    smsInArr[i, 2] = myReader.GetString(2);   //Message
                    smsInArr[i, 3] = myReader.GetString(3);   //timestamp
                    i++;
                }

                arrSize = i;

            }

            catch (Exception e)
            {
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                Thread.Sleep(120000); // 2 min
            }

            finally
            {
                myReader.Close();
                myConnection.Close();
            }

        }  //end ReadSmsInTable

        public void ReadAutoHTable(string mySelectQuery, out string myStatusMessage, bool postChange)
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
            catch (Exception e)
            {
                /**/ Thread.Sleep(120000); // 2 min
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                /**/
                return;
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

            catch (Exception e)
            {
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                Thread.Sleep(120000); // 2 min
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

        }  //end ReadSmsInTable

        public void UpdateSmsOut(string myInsertQuery)
        {
            MySqlConnection myConnection = new MySqlConnection(connectionString);
            MySqlCommand myCommand = new MySqlCommand(myInsertQuery);
            myCommand.Connection = myConnection;

            try
            {
                myConnection.Open();
                myCommand.ExecuteNonQuery();
            }

            catch (Exception e)
            {
                myLog.WriteLog("DB ERROR UpdateSmsOut - will wait for 1 min before retrying:" + e);
                Thread.Sleep(60000); // 2 min
            }

            finally
            {
                myConnection.Close();
                myCommand.Connection.Close();
            }

        }

        public void GeneralInsertUpdate(string myInsertQuery)
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

            catch (Exception e)
            {
                //log the problem. if the insert doesn't work it will select again and keep trying
                myLog.WriteLog("DB ERROR UpdateSmsOut - will wait for 1 min before retrying:" + e);
            }

            finally
            {
                myConnection.Close();
                myCommand.Connection.Close();
            }

        }

        public bool GetAppStats(string mySelectQuery, out string[,] mode1array, ref string[] nisEnv, ref string[,] apps)
        {
            int arraySize = (nisEnv.Length * (apps.Length / 2)) - 2; //minus 2 as there is no sass meta in nis4

            //declare a template array
            mode1array = new string[arraySize, 9];

            int nisLen = nisEnv.Length;
            int appLen = apps.Length / 2;

            for (int i = 0; i < nisLen; i++)
            {

                if (i == 1)

                    for (int j = 0; j < appLen; j++)
                    {
                        if ((i == 1 && j == 5) || (i == 1 && j == 6))
                            continue;

                        mode1array[((i * appLen) + j), 0] = nisEnv[i];
                        mode1array[((i * appLen) + j), 1] = apps[0, j];
                        mode1array[((i * appLen) + j), 2] = apps[1, j];
                    }
            }


            //prep pop array

            MySqlConnection myConnection = new MySqlConnection(connectionString);

            MySqlDataAdapter adapter = new MySqlDataAdapter();

            DataSet myDataSet = new DataSet();

            adapter.SelectCommand = new MySqlCommand(mySelectQuery, myConnection);
            adapter.Fill(myDataSet, "myTable");

            //TODO: return myDataSet?

            DateTime timeNow = DateTime.Now;

            foreach (DataTable myTable in myDataSet.Tables)
            {
                foreach (DataRow myRow in myTable.Rows)
                {

                    //applicationnames.mmid, applicationnames.short_app_name, app_availability, start_time, Environment



                    //Console.WriteLine(myRow["mmid"] + "|" +
                    //                  myRow["short_app_name"] + "|" + 
                    //                  myRow["app_availability"] + "|"+ 
                    //                  myRow["start_time"] + "|"+ 
                    //                  //myRow["end_time"] + "|"+ 
                    //                  myRow["Environment"]);


                    int nisEnvResult = -1;

                    //which environment is being used
                    for (int i = 0; i < nisLen; i++)
                    {
                        if (nisEnv[i] == Convert.ToString(myRow["Environment"]))
                        {
                            nisEnvResult = i;
                            break;
                        }
                    }

                    if (nisEnvResult < 0)
                        continue;

                    int arrayLoc = -1;

                    //find array element to use
                    for (int i = (nisEnvResult * appLen); i < ((nisEnvResult * appLen) + appLen); i++)
                    {
                        if (mode1array[i, 2] == Convert.ToString(myRow["mmid"]))
                        {
                            arrayLoc = i;
                            break;
                        }
                    }

                    if (arrayLoc < 0)
                        continue;

                    //we now know that the mode1array row is arrayLoc so need to populate the results cells
                    int particularHour = -1;
                    DateTime timeResult = Convert.ToDateTime(myRow["start_time"]);

                    DateTime hrPlus1 = timeNow.AddHours(-1);
                    DateTime hrPlus2 = timeNow.AddHours(-2);
                    DateTime hrPlus3 = timeNow.AddHours(-3);
                    DateTime hrPlus4 = timeNow.AddHours(-4);
                    DateTime hrPlus5 = timeNow.AddHours(-5);
                    DateTime hrPlus6 = timeNow.AddHours(-6);

                    if (timeResult > hrPlus1)
                        particularHour = 3;

                    else if (timeResult < hrPlus1 && timeResult > hrPlus2)
                        particularHour = 4;

                    else if (timeResult < hrPlus2 && timeResult > hrPlus3)
                        particularHour = 5;

                    else if (timeResult < hrPlus3 && timeResult > hrPlus4)
                        particularHour = 6;

                    else if (timeResult < hrPlus4 && timeResult > hrPlus5)
                        particularHour = 7;

                    else if (timeResult < hrPlus5 && timeResult > hrPlus6)
                        particularHour = 8;

                    //contine if no match - this can occur if timeResult a few seconds out of 6hr limit
                    if (particularHour <= 0)
                        continue;

                    //update the correct cell
                    string myNewStatus = "";

                    if (Convert.ToString(myRow["app_availability"]) == "1")
                        myNewStatus = "G";
                    else
                        myNewStatus = "R";


                    //if new update is the same as already recorded then contine
                    if (mode1array[arrayLoc, particularHour] == myNewStatus)
                        continue;


                    if ((mode1array[arrayLoc, particularHour] == "G" && myNewStatus == "R") ||
                            (mode1array[arrayLoc, particularHour] == "R" && myNewStatus == "G"))
                    {
                        mode1array[arrayLoc, particularHour] = "A"; //set to amber
                        continue;
                    }

                    mode1array[arrayLoc, particularHour] = myNewStatus;


                    //application_id, app_availability, start_time, end_time, Environment

                    //foreach (DataColumn myColumn in myTable.Columns)
                    //{
                    //    Console.WriteLine(myRow[myColumn]);
                    //}
                } // end myRow foreach
            } //end myTable foreach


            //if the db is unavailable then it's going to crash here
            try
            {

            }
            catch (Exception e)
            {
                /**/ Thread.Sleep(120000); // 2 min
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                /**/
                return false;
            }


            return true;


        }  //end  ReadSmsOutTable

        public bool CheckPhoneNumber(string mySelectQuery, out string autohId)
        {
            bool phoneNumberValid = false;
            autohId = "";

            MySqlConnection myConnection = new MySqlConnection(connectionString);
            MySqlCommand myCommand = new MySqlCommand(mySelectQuery, myConnection);
            MySqlDataReader myReader;

            //if the db is unavailable then it's going to crash here
            try
            {
                myConnection.Open();
                myReader = myCommand.ExecuteReader();
            }
            catch (Exception e)
            {
                /**/ Thread.Sleep(120000); // 2 min
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                /**/
                return false;
            }

            try
            {
                while (myReader.Read())
                {
                    phoneNumberValid = true;
                    autohId = myReader.GetString(0); //count of phone numbers
                    break; // just take the first match. no functionality for multiple entries yet
                }
            }

            catch (Exception e)
            {
                myLog.WriteLog("DB ERROR - will wait for 2 mins before retrying:" + e);
                Thread.Sleep(120000); // 2 min
                return false;
            }

            finally
            {
                myReader.Close();
                myConnection.Close();
            }

            return phoneNumberValid;

        }  //end ReadSmsInTable

    } //end .class DbClass

    class GsmClass
    {
        private static GsmOut objGsmOut = null;
        private static GsmIn objGsmIn = null;
        private static SmsConstants objConstants = null;

        //AlertSMS myAlertSMS = new AlertSMS();
        Logger myLog = new Logger();

        public bool GsmSendMessages(ref string[,] smsOutMessArray, int smsOutArSz)
        {
            //create a local instance of MyClass
            DbClass gsmDbClass = new DbClass();

            //init the GSM to prep for sending messages
            objGsmOut = new GsmOut();
            objConstants = new SmsConstants();

            string strDevice = "";
            int foundModem = 0;

            System.Int32 numDeviceCount, l;

            numDeviceCount = objGsmOut.GetDeviceCount();
            for (l = 0; l < numDeviceCount; l++)
            {
                strDevice = objGsmOut.GetDevice(l);

                if (strDevice == "Teltonika ModemUSB G10")
                {
                    foundModem = 1;
                    objGsmOut.Device = strDevice;
                    break;
                }
            }

            if (foundModem == 0)
            {
                //disable some stuff and put up an error message
                myLog.WriteLog("Unable to connect to \"Teltonika ModemUSB G10\" modem");

                return false;
            }

            // Device settings
            objGsmOut.Device = strDevice;
            bool smsSuccessFlag = true;
            int retry = 0;
            int maxRetry = 8;

            while (true)
            {
                for (int i = 0; i < smsOutArSz; i++)
                {
                    //if there has been a failure
                    if (retry > 0 && smsOutMessArray[i, 4] == "1")
                    {
                        continue;
                    }

                    if (i > 0)
                        Thread.Sleep(500); // half sec sleep to give modem a chance

                    if (retry > 0)
                        Thread.Sleep(3000); // add another 3 sec if in retry mode

                    string eventString = "AlertSMS summary log info for GSM";

                    if (retry > 0)
                        eventString += " (RETRY)"; 
 
                    eventString +=  "|" + smsOutMessArray[i, 0] + ", " + smsOutMessArray[i, 1]
                                        + ", " + smsOutMessArray[i, 2] + ", " + smsOutMessArray[i, 3] + ", " + smsOutMessArray[i, 4];

                    //send message here
                    objGsmOut.DeviceSpeed = 0;

                    // Message Settings
                    objGsmOut.MessageRecipient = "+" + smsOutMessArray[i, 1];
                    objGsmOut.MessageData = smsOutMessArray[i, 2];

                    //check size of message
                    string message = smsOutMessArray[i, 2];

                    if (message.Length > 160)
                        objGsmOut.MessageType = objConstants.asMESSAGETYPE_TEXT_MULTIPART;
                    else
                        objGsmOut.MessageType = objConstants.asMESSAGETYPE_TEXT;

                    objGsmOut.Send();

                    eventString += "|Result:" + objGsmOut.LastError.ToString() + ": " + objGsmOut.GetErrorDescription(objGsmOut.LastError);

                    string updateString;

                    if (objGsmOut.LastError != 0)
                    {
                        //failed to send message
                        updateString = "UPDATE SmsOut SET sent = 0 WHERE SmsOutId = " + smsOutMessArray[i, 0];
                        smsOutMessArray[i, 4] = "0";
                        smsSuccessFlag = false;
                    }
                    else
                    {
                        //Success!!
                        updateString = "UPDATE SmsOut SET sent = 1 WHERE SmsOutId = " + smsOutMessArray[i, 0];
                        smsOutMessArray[i, 4] = "1";
                    }

                    eventString += "|" + updateString;

                    if (retry > 0)
                    {
                        eventString += "|retry number" + Convert.ToString(retry) + "|";
                    }

                    myLog.WriteLog(eventString);
                    gsmDbClass.UpdateSmsOut(updateString); //update db of failure after max number of retries


                } //end for

                //keep trying to send the failing messages until retry limit reached
                if (smsSuccessFlag == false && retry < maxRetry)
                {
                    smsSuccessFlag = true;
                    retry++; //retry mode
                    continue;
                }

                break; //while

            } //end while

            //will return false if there is a failure to send message
            return smsSuccessFlag;

            //TODO: Write a truncate method to remove messages from SmsOut table older than one month or date + 30 days

        }

        public bool GsmRetrieveMessages()
        {
            //create a local instance of MyClass
            DbClass gsmDbClassIn = new DbClass();

            objGsmIn = new GsmIn();
            objConstants = new SmsConstants();

            System.Int32 numMessageType;
            objGsmIn.DeviceSpeed = 0;

            string strDevice = "";
            int foundModem = 0;

            System.Int32 numDeviceCount, l;

            numDeviceCount = objGsmIn.GetDeviceCount();
            for (l = 0; l < numDeviceCount; l++)
            {
                strDevice = objGsmIn.GetDevice(l);

                if (strDevice == "Teltonika ModemUSB G10")
                {
                    foundModem = 1;
                    objGsmIn.Device = strDevice;
                    break;
                }
            }

            if (foundModem == 0)
            {
                //disable some stuff and put up an error message
                myLog.WriteLog("Unable to connect to \"Teltonika ModemUSB G10\" modem");

                return false;
            }

            // Device settings
            objGsmIn.Device = strDevice;

            // Set Storage
            objGsmIn.Storage = 2;

            // Set Delete on/off
            objGsmIn.DeleteAfterReceive = 1; // 0 = no delete; 1 = delete messages;

            // Set Cursor
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

            // Read Items from storage
            objGsmIn.Receive();

            //myLog.WriteLog("LastError:" + objGsmIn.LastError);

            if (objGsmIn.LastError == 0)
            {
                objGsmIn.GetFirstMessage();

                while (objGsmIn.LastError == 0)
                {
                    numMessageType = objGsmIn.MessageType;
                    if (numMessageType != objConstants.asMESSAGETYPE_DATA)
                    {
                        myLog.WriteLog(objGsmIn.MessageSender + "#" + objGsmIn.MessageData);


                        string temp = objGsmIn.MessageSender;
                        string myMessageSender = "";

                        //remove + from the number if it exists
                        if (temp.Substring(0, 1) == "+")
                        {
                            //string checkForPlus = temp[1];
                            myMessageSender = temp.Substring(1, temp.Length - 1);
                        }
                        else
                            myMessageSender = objGsmIn.MessageSender;

                        //insert sender and message into the smsin table
                        gsmDbClassIn.GeneralInsertUpdate("INSERT INTO autoharness.smsin ( number, message, timestamp, status) VALUES ( '"
                                                + myMessageSender + "', '" + objGsmIn.MessageData + "', now(), -1)");

                    }
                    else
                        //TODO: write to error log
                        myLog.WriteLog(objGsmIn.MessageSender + "#" + "<DATA>");

                    objGsmIn.GetNextMessage();
                }
            }

            // Reset Cursor
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

            return true;
        }

    } //end class GsmClass

    class Click_A_TellGsmClass
    {
        //bool outputValue = false;

        public void ClickASendMessages(ref string[,] smsOutMessArray, int smsOutArSz)
        {
            //ERR: 001, Authentication failed
            //ID: f9fee9fee0cb202d922b706f68c75922

            //create a local instance of MyClass
            DbClass catDbClass = new DbClass();
            //AlertSMS myAlertSMS = new AlertSMS();
            Logger myLog = new Logger();


            for (int i = 0; i < smsOutArSz; i++)
            {
                //ignore if message has been successfully sent via GSM
                if (smsOutMessArray[i, 4] == "1")
                    continue;

                string eventString = "Unable to send message|" + smsOutMessArray[i, 0] + ", " + smsOutMessArray[i, 1]
                                    + ", " + smsOutMessArray[i, 2] + ", " + smsOutMessArray[i, 3] + ", " + smsOutMessArray[i, 4];

                ////outputValue = true;
                //WebRequest objRequest;
                //HttpWebResponse resp;

                //string outMessage = smsOutMessArray[i, 2].Replace("%", "%%");
                //string clickConcat = "";

                ////if a multiple message
                //if (outMessage.Length > 160)
                //{
                //    double mLength = (double)outMessage.Length;
                //    double concatMessageMax = 153;
                //    double d = mLength / concatMessageMax;

                //    int iLength = (int)Math.Ceiling(d);
                //    clickConcat = "&concat=" + Convert.ToString(iLength);
                //}

                ////The web request to be sent to the message gateway.
                //string msgRequest = "http://api.clickatell.com/http/sendmsg?user=iqeitg&password=r3dh4t&api_id=1245143&to=" + smsOutMessArray[i, 1] + "&text=" + outMessage + clickConcat;

                ////Send the message to the individual on tech. support.
                //objRequest = System.Net.HttpWebRequest.Create(msgRequest);
                //resp = (HttpWebResponse)objRequest.GetResponse();

                ////string s;

                //Stream stream = resp.GetResponseStream();
                //StreamReader sr = new StreamReader(stream);

                //string s = sr.ReadLine();

                string updateString;

                //if (s.StartsWith("ERR:"))
                //{
                    //failed to send message
                    updateString = "UPDATE SmsOut SET sent = -2 WHERE SmsOutId = " + smsOutMessArray[i, 0];
                //}
                //else
                //{
                    //Success!!
                    //updateString = "UPDATE SmsOut SET sent = 2 WHERE SmsOutId = " + smsOutMessArray[i, 0];
                //}

                eventString += "|" + updateString;
                myLog.WriteLog(eventString);
                catDbClass.UpdateSmsOut(updateString);

            }//end for

        } //end ClickASendMessages

    } //end class Click_A_TellGsmClass
    //###### End Add Classes here ######

}
