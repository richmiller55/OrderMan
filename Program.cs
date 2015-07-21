using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Reflection;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;

using System.Configuration;
using OrderMan.Epicor.SessionModSvc;
using OrderMan.Epicor.SalesOrderSvc;

namespace OrderMan
{
 
    class Program
    {
        private static string epiUser = "manager";
        private static string epiPassword = "manager";
        private static string epiServer = "ITEP10";
        private static string epiSite = "Epicor10Production";

        private enum EndpointBindingType
        {
            SOAPHttp,
            BasicHttp
        }
        [STAThread]
        static void Main()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => { return true; };

            EndpointBindingType bindingType = EndpointBindingType.BasicHttp;

            string epicorUserID = epiUser;
            string epiorUserPassword = epiPassword;

            string scheme = "http";
            if (bindingType == EndpointBindingType.BasicHttp)
            {
                scheme = "https";
            }
            UriBuilder builder = new UriBuilder(scheme, epiServer);

            builder.Path = epiSite + "/Ice/Lib/SessionMod.svc";

            SessionModSvcContractClient sessionModClient = GetClient<SessionModSvcContractClient, SessionModSvcContract>(builder.Uri.ToString(), epicorUserID, epiorUserPassword, bindingType);

            builder.Path = epiSite + "/Erp/BO/SalesOrder.svc";
            SalesOrderSvcContractClient salesOrderClient = GetClient<SalesOrderSvcContractClient, SalesOrderSvcContract>(builder.Uri.ToString(), epiorUserPassword, epicorUserID, bindingType);

            Guid sessionId = Guid.Empty;

            try
            {
                sessionId = sessionModClient.Login();
                sessionModClient.Endpoint.Behaviors.Add(new HookServiceBehavior(sessionId, epicorUserID));
                salesOrderClient.Endpoint.Behaviors.Add(new HookServiceBehavior(sessionId, epicorUserID));

                string dirName = @"Z:\e10\EDI_Data\orderAdj";
                string[] filePaths = Directory.GetFiles(dirName);
                
                foreach (string fileName in filePaths)
                {
                    try
                    {
                        StreamReader tr = new StreamReader(Path.Combine(dirName, fileName));
                        string orderChangeLine = ""; // tab delimited junk for this job
                        while ((orderChangeLine = tr.ReadLine()) != null)
                        {
                            string result = "";
                            UpdateSalesOrder(salesOrderClient, orderChangeLine, out result);
                        }
                    }
                    catch (Exception e)
                    {
                        string message = e.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ex" + ex.Message);
                sessionModClient.Logout();
            }
            if (sessionId != Guid.Empty)
            {
                sessionModClient.Logout();
            }
        }
        private static WSHttpBinding GetWsHttpBinding()
        {
            var binding = new WSHttpBinding();
            const int maxBindingSize = Int32.MaxValue;
            binding.MaxReceivedMessageSize = maxBindingSize;
            binding.ReaderQuotas.MaxDepth = maxBindingSize;
            binding.ReaderQuotas.MaxStringContentLength = maxBindingSize;
            binding.ReaderQuotas.MaxArrayLength = maxBindingSize;
            binding.ReaderQuotas.MaxBytesPerRead = maxBindingSize;
            binding.ReaderQuotas.MaxNameTableCharCount = maxBindingSize;
            binding.Security.Mode = SecurityMode.Message;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            return binding;
        }

        public static BasicHttpBinding GetBasicHttpBinding()
        {
            var binding = new BasicHttpBinding();
            const int maxBindingSize = Int32.MaxValue;
            binding.MaxReceivedMessageSize = maxBindingSize;
            binding.ReaderQuotas.MaxDepth = maxBindingSize;
            binding.ReaderQuotas.MaxStringContentLength = maxBindingSize;
            binding.ReaderQuotas.MaxArrayLength = maxBindingSize;
            binding.ReaderQuotas.MaxBytesPerRead = maxBindingSize;
            binding.ReaderQuotas.MaxNameTableCharCount = maxBindingSize;
            binding.Security.Mode = BasicHttpSecurityMode.TransportWithMessageCredential;
            binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
            return binding;
        }

        private static TClient GetClient<TClient, TInterface>(string url, string username, string password, EndpointBindingType bindingType)
            where TClient : ClientBase<TInterface>
            where TInterface : class
        {
            System.ServiceModel.Channels.Binding binding = null;
            TClient client;
            var endpointAddress = new EndpointAddress(url);
            switch (bindingType)
            {
                case EndpointBindingType.BasicHttp:
                    binding = GetBasicHttpBinding();
                    break;
                case EndpointBindingType.SOAPHttp:
                    binding = GetWsHttpBinding();
                    break;
            }
            TimeSpan operationTimeout = new TimeSpan(0, 12, 0);
            binding.CloseTimeout = operationTimeout;
            binding.ReceiveTimeout = operationTimeout;
            binding.SendTimeout = operationTimeout;
            binding.OpenTimeout = operationTimeout;

            client = (TClient)Activator.CreateInstance(typeof(TClient), binding, endpointAddress);
            if (!string.IsNullOrEmpty(username) && (client.ClientCredentials != null))
            {
                client.ClientCredentials.UserName.UserName = username;
                client.ClientCredentials.UserName.Password = password;
            }
            return client;
        }
        static void UpdateSalesOrder(SalesOrderSvcContractClient salesOrderClient, string orderChange, out string result)
        {
            string[] split = orderChange.Split(new Char[] { '\t' });
            string strOrderNum = split[(int)layout.orderNum];
            int OrderNum = System.Convert.ToInt32(strOrderNum);
    
            var ts = new SalesOrderTableset();
            ts = salesOrderClient.GetByID(OrderNum);
            result = "started process for --> ";
            if (ts != null)
            {
                result += strOrderNum;
                var DtlRow = ts.OrderDtl.Where(n => n.PartNum.Equals("757026287334", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                DtlRow.RevisionNum = "PCK";    
                
                DtlRow.RowMod = "U";
                try
                {
                    salesOrderClient.Update(ref ts);
                    ts = salesOrderClient.GetByID(OrderNum);
                }
                catch (Exception ex2)
                {
                    string mess2 = ex2.Message;
                    // result = ex2.Message;
                }

            }
        }
    
    }
}
