﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.Azure.Test.HttpRecorder;
using Microsoft.Azure.Portal.RecoveryServices.Models.Common;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Commands.ScenarioTest;
using Microsoft.WindowsAzure.Commands.Utilities.Common;
using Microsoft.WindowsAzure.Management.Scheduler;
using Microsoft.Azure.Management.RecoveryServices;
using Microsoft.Azure.Management.SiteRecovery;
using Microsoft.Azure.Test;
using Microsoft.WindowsAzure.Commands.Common;
using Microsoft.Azure.Common.Authentication.Models;
using Microsoft.Azure.Common.Authentication;

namespace Microsoft.Azure.Commands.SiteRecovery.Test.ScenarioTests
{
    public abstract class SiteRecoveryTestsBase
    {
        private CSMTestEnvironmentFactory armTestFactory;
        private EnvironmentSetupHelper helper;
        protected string vaultSettingsFilePath;
        private ASRVaultCreds asrVaultCreds = null;

        public SiteRecoveryManagementClient SiteRecoveryMgmtClient { get; private set; }
        public RecoveryServicesManagementClient RecoveryServicesMgmtClient { get; private set; }
        public CloudServiceManagementClient CloudServiceManagementClient { get; private set; }

        protected SiteRecoveryTestsBase()
        {
            this.vaultSettingsFilePath = "ScenarioTests\\vaultSettings.VaultCredentials";

            if (File.Exists(this.vaultSettingsFilePath))
            {
                try
                {
                    var serializer1 = new DataContractSerializer(typeof(ASRVaultCreds));
                    using (var s = new FileStream(
                        this.vaultSettingsFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                    {
                        asrVaultCreds = (ASRVaultCreds)serializer1.ReadObject(s);
                    }
                }
                catch (XmlException xmlException)
                {
                    throw new XmlException(
                        "XML is malformed or file is empty", xmlException);
                }
                catch (SerializationException serializationException)
                {
                    throw new SerializationException(
                        "XML is malformed or file is empty", serializationException);
                }
            }
            else
            {
                throw new FileNotFoundException(
                    "Vault settings file not found, please pass the file downloaded from portal");
            }

            helper = new EnvironmentSetupHelper();
        }

        protected void SetupManagementClients()
        {
            CloudServiceManagementClient = GetCloudServicesManagementClient();
            RecoveryServicesMgmtClient = GetRecoveryServicesManagementClient();
            SiteRecoveryMgmtClient = GetSiteRecoveryManagementClient();

            helper.SetupManagementClients(CloudServiceManagementClient, RecoveryServicesMgmtClient, SiteRecoveryMgmtClient);
        }

        protected void RunPowerShellTest(params string[] scripts)
        {
            using (UndoContext context = UndoContext.Current)
            {
                context.Start(TestUtilities.GetCallingClass(2), TestUtilities.GetCurrentMethodName(2));

                this.armTestFactory = new CSMTestEnvironmentFactory();

                SetupManagementClients();

                helper.SetupEnvironment(AzureModule.AzureResourceManager);
                helper.SetupModules(AzureModule.AzureResourceManager,
                    "ScenarioTests\\" + this.GetType().Name + ".ps1");

                helper.RunPowerShellTest(scripts);
            }
        }

        private CloudServiceManagementClient GetCloudServicesManagementClient()
        {
            return TestBase.GetServiceClient<CloudServiceManagementClient>(this.armTestFactory);
        }

        private RecoveryServicesManagementClient GetRecoveryServicesManagementClient()
        {
            return new RecoveryServicesManagementClient(
                "Microsoft.SiteRecovery",
                CloudServiceManagementClient.Credentials,
                CloudServiceManagementClient.BaseUri).WithHandler(HttpMockServer.CreateInstance());
        }

        private SiteRecoveryManagementClient GetSiteRecoveryManagementClient()
        {
            TestEnvironment environment = this.armTestFactory.GetTestEnvironment();

            if (ServicePointManager.ServerCertificateValidationCallback == null)
            {
                ServicePointManager.ServerCertificateValidationCallback =
                    IgnoreCertificateErrorHandler;
            }

            return new SiteRecoveryManagementClient(
                asrVaultCreds.ResourceName,
                asrVaultCreds.ResourceGroupName,
                "Microsoft.SiteRecovery",
                CloudServiceManagementClient.Credentials,
                CloudServiceManagementClient.BaseUri).WithHandler(HttpMockServer.CreateInstance());
        }

        private static bool IgnoreCertificateErrorHandler
           (object sender,
           System.Security.Cryptography.X509Certificates.X509Certificate certificate,
           System.Security.Cryptography.X509Certificates.X509Chain chain,
           SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}