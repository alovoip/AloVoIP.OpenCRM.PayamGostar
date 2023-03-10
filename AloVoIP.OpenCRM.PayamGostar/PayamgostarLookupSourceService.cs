using AloVoIP.OpenCRM.PayamGostar.Helper;
using AloVoIP.OpenCRM.Requests;
using AloVoIP.OpenCRM.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PgContractService;
using PgCrmObjectService;
using PgCrmObjectType;
using PgEPayService;
using PgIdentityService;
using PgInvoiceService;
using PgMoneyAccountService;
using PgUserService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AloVoIP.OpenCRM.PayamGostar
{
    public class PayamgostarLookupSourceService : PayamGostarCallStoreService, ILookupSourceService
    {
        public PayamgostarLookupSourceService(string lookupSourceId, string host, string username, string password)
            : base(lookupSourceId, host, username, password)
        {
        }

        public ICrmObjectTypeChannel CreateCrmObjectTypeClient()
        {
            return new PayamgostarServiceClientFactory<ICrmObjectTypeChannel>().Create(Host);
        }
        public ICrmObjectChannel CreateCrmObjectChannelClient()
        {
            return new PayamgostarServiceClientFactory<ICrmObjectChannel>().Create(Host);
        }
        public IIdentityChannel CreateIdentityChannelClient()
        {
            return new PayamgostarServiceClientFactory<IIdentityChannel>().Create(Host);
        }
        public IUserChannel CreateUserChannelClient()
        {
            return new PayamgostarServiceClientFactory<IUserChannel>().Create(Host);
        }
        public IContractChannel CreateContractChannelClient()
        {
            return new PayamgostarServiceClientFactory<IContractChannel>().Create(Host);
        }
        public IEpayChannel CreateEpayClient()
        {
            return new PayamgostarServiceClientFactory<IEpayChannel>().Create(Host);
        }
        public IInvoiceChannel CreateInvoiceClient()
        {
            return new PayamgostarServiceClientFactory<IInvoiceChannel>().Create(Host);
        }
        public IMoneyAccountChannel CreateMoneyAccountClient()
        {
            return new PayamgostarServiceClientFactory<IMoneyAccountChannel>().Create(Host);
        }

        private PaymentResponse GetInvoiceInfo(CustomerRequest customerRequest, string billableObjectTypeKey, string billableObjectNumber, string lookupNumberFieldKey, string valueFieldKey)
        {
            PaymentResponse paymentResponse = null;
            var query = string.Empty;

            if (customerRequest != null)
            {
                Guid customerId;
                if (Guid.TryParse(customerRequest.CustomerId, out customerId))
                {
                    query = $"IdentityId==\"{customerId}\" & ";
                }
            }

            // LookupNumberFieldKey
            if (string.IsNullOrEmpty(lookupNumberFieldKey))
                query = $"Number==\"{billableObjectNumber}\"";
            else
                query = $"{lookupNumberFieldKey}==\"{billableObjectNumber}\"";

            using (var invoiceChannel = CreateInvoiceClient())
            {
                var invoiceInfoResult = invoiceChannel.SearchInvoice(Username,
                                                                     Password,
                                                                     billableObjectTypeKey,
                                                                     query);
                if (invoiceInfoResult.Success &&
                    invoiceInfoResult.InvoiceInfoList != null &&
                    invoiceInfoResult.InvoiceInfoList.Length > 0)
                {
                    var invoice = invoiceInfoResult.InvoiceInfoList[0];


                    paymentResponse = new PaymentResponse();
                    if (invoice.IdentityId.HasValue)
                        paymentResponse.IdentityId = invoice.IdentityId.Value.ToString();

                    if (string.IsNullOrEmpty(valueFieldKey))
                    {
                        paymentResponse.Amount = invoice.FinalValue;
                    }
                    else
                    {
                        decimal res = 0;
                        var invoinceByLookupField = invoice.ExtendedProperties.FirstOrDefault(x => x.UserKey == lookupNumberFieldKey);
                        if (invoinceByLookupField != null)
                        {
                            var invoinceByValueField = invoice.ExtendedProperties.FirstOrDefault(x => x.UserKey == valueFieldKey);
                            if (decimal.TryParse(invoinceByValueField.Value.ToString(), out res))
                            {
                                paymentResponse.Amount = res;
                            }
                        }
                    }
                }

                return paymentResponse;
            }
        }
        private PaymentResponse GetContractInfo(CustomerRequest customerRequest, string billableObjectTypeKey, string billableObjectNumber, string lookupNumberFieldKey, string valueFieldKey)
        {
            PaymentResponse paymentResponse = null;

            var query = string.Empty;

            if (customerRequest != null)
            {
                Guid customerId;
                if (Guid.TryParse(customerRequest.CustomerId, out customerId))
                {
                    query = $"IdentityId==\"{customerId}\" & ";
                }
            }

            // LookupNumberFieldKey
            if (string.IsNullOrEmpty(lookupNumberFieldKey))
                query = $"Number==\"{billableObjectNumber}\"";
            else
                query = $"{lookupNumberFieldKey}==\"{billableObjectNumber}\"";

            using (var contractChannel = CreateContractChannelClient())
            {
                var contractInfoResult = contractChannel.SearchContract(Username,
                                                                        Password,
                                                                        billableObjectTypeKey,
                                                                        query);
                if (contractInfoResult.Success &&
                    contractInfoResult.ContractInfoList != null &&
                    contractInfoResult.ContractInfoList.Length > 0)
                {
                    paymentResponse = new PaymentResponse();

                    if (contractInfoResult.ContractInfoList[0].IdentityId.HasValue)
                        paymentResponse.IdentityId = contractInfoResult.ContractInfoList[0].IdentityId.Value.ToString();

                    if (string.IsNullOrEmpty(valueFieldKey))
                    {
                        paymentResponse.Amount = contractInfoResult.ContractInfoList[0].FinalValue;
                    }
                    else
                    {
                        decimal res = 0;
                        var contractInfoType = contractInfoResult.ContractInfoList[0].GetType();
                        if (decimal.TryParse(contractInfoType.GetProperty(valueFieldKey).GetValue(contractInfoResult.ContractInfoList[0]).ToString(), out res))
                            paymentResponse.Amount = res;
                    }
                }

                return paymentResponse;
            }
        }
        public async Task<BillableObjectTypesResponse> GetBillableObjectTypes()
        {
            try
            {
                using (var crmObjectType = CreateCrmObjectTypeClient())
                {
                    var lstCrmObjectType = crmObjectType.GetCrmObjectTypeList(Username, Password, null);
                    if (lstCrmObjectType != null && lstCrmObjectType.CrmObjectTypeList != null && lstCrmObjectType.CrmObjectTypeList.Length > 0)
                    {
                        var crmObjectTypes = new[]
                        {
                        CrmObjectTypes.Invoice,
                        CrmObjectTypes.Quote,
                        CrmObjectTypes.Receipt,
                        CrmObjectTypes.Contract,
                        CrmObjectTypes.PurchaseInvoice,
                        CrmObjectTypes.ReturnPurchaseInvoice,
                        CrmObjectTypes.ReturnSaleInvoice,
                        CrmObjectTypes.PurchaseQuote,
                        CrmObjectTypes.Payment
                    };

                        var lstBillableObjects = lstCrmObjectType.CrmObjectTypeList.Where(x => crmObjectTypes.Contains(x.CrmObjectType) &&
                                                                  x.UserKey != "");
                        if (lstBillableObjects != null)
                        {
                            return await Task.FromResult(new BillableObjectTypesResponse
                            {
                                CRMObjectTypes = lstBillableObjects.Select(x => new CrmObjectTypeResponse
                                {
                                    Key = x.UserKey,
                                    Name = x.Name
                                }).ToList()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in GetBillableObjectTypes.");
            }

            return null;
        }
        public async Task<MoneyAccountsResponse> GetMoneyAccounts()
        {
            try
            {
                using (var moneyAccountChannel = CreateMoneyAccountClient())
                {
                    var moneyAccountListInfo = moneyAccountChannel.GetMoneyAccountList(Username, Password, 0, 50);
                    if (moneyAccountListInfo != null && moneyAccountListInfo.Items != null && moneyAccountListInfo.Items.Length > 0)
                    {
                        var lstmoneyAccounts = moneyAccountListInfo.Items.Where(x => x.UserKey != "");
                        if (lstmoneyAccounts != null)
                        {
                            return await Task.FromResult(new MoneyAccountsResponse
                            {
                                MoneyAccounts = lstmoneyAccounts.Select(x => new MoneyAccountResponse
                                {
                                    Key = x.UserKey,
                                    Name = x.Name
                                }).ToList()
                            });
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in GetMoneyAccounts");
            }

            return null;
        }
        public async Task<IdentityResponse> GetIdentityByCustomerInfo(CustomerRequest customerRequest)
        {
            try
            {
                Guid customerId;
                if (Guid.TryParse(customerRequest.CustomerId, out customerId))
                {
                    using (var identityChannel = CreateIdentityChannelClient())
                    {
                        return await Task.FromResult(identityChannel.FindIdentityById(Username, Password, customerId).IdentityInfo.ToDto());
                    }
                }
                if (string.IsNullOrEmpty(customerRequest.CustomerNo))
                {
                    return await GetIdentityByCustomerNumber(new IdentityByCustomerNumberRequest { CustomerNumber = customerRequest.CustomerNo });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in GetIdentityByCustomerInfo. customerRequest: {@customerRequest}", customerRequest);
            }

            Log.Debug("GetIdentityByCustomerInfo result is null. {@customerRequest}", customerRequest);
            return null;
        }
        public Task<IdentityResponse> GetIdentityByPhoneNumber(IdentityByPhoneNumberRequest identityByPhoneNumberRequest)
        {
            try
            {
                using (var identityChannel = CreateIdentityChannelClient())
                {
                    return Task.FromResult(identityChannel.FindIdentityByPhoneNumber(Username, Password, identityByPhoneNumberRequest.PhoneNumber).IdentityInfo.ToDto());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetIdentityByPhoneNumber. phoneNumber:{identityByPhoneNumberRequest.PhoneNumber}");
            }

            Log.Debug($"GetIdentityByPhoneNumber, FindIdentityByPhoneNumber result is null. {nameof(identityByPhoneNumberRequest.PhoneNumber)}:{identityByPhoneNumberRequest.PhoneNumber}");
            return null;
        }
        public async Task<IdentityResponse> GetIdentityByCustomerNumber(IdentityByCustomerNumberRequest identityByCustomerNumberRequest)
        {
            try
            {
                using (var identityChannel = CreateIdentityChannelClient())
                {
                    var result = identityChannel.SearchIdentity(Username, Password, string.Empty,
                        $"CustomerNumber==\"{identityByCustomerNumberRequest.CustomerNumber}\"");
                    if (result.Success)
                    {
                        if (result.IdentityInfoList.Length == 0)
                        {
                            Log.Debug($"GetIdentityByCustomerNumber, SearchIdentity result is success but identityInfoList is empty." +
                                $" {nameof(identityByCustomerNumberRequest.CustomerNumber)}:{identityByCustomerNumberRequest.CustomerNumber}");
                            return null;
                        }
                        else
                            return await Task.FromResult(result.IdentityInfoList[0].ToDto());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetIdentityByCustomerNumber. customerNumber:{identityByCustomerNumberRequest.CustomerNumber}");
            }

            Log.Debug($"GetIdentityByCustomerNumber, SearchIdentity result is null. {nameof(identityByCustomerNumberRequest.CustomerNumber)}:{identityByCustomerNumberRequest.CustomerNumber}");
            return null;
        }
        public async Task<IdentityHasValidContractResponse> IdentityHasValidContract(IdentityHasValidContractRequest identityHasValidContractRequest)
        {
            try
            {
                using (var contractChannel = CreateContractChannelClient())
                {
                    var contracts = contractChannel.SearchContract(Username, Password, identityHasValidContractRequest.ContractKey,
                        $"IdentityId==\"{identityHasValidContractRequest.CustomerRequest.CustomerId}\"");
                    if (!contracts.Success)
                    {
                        Log.Debug("IdentityHasValidContract, SearchContract result is not success. customerInfo:{@customerInfo}, contractKey:{contractKey}",
                            identityHasValidContractRequest.CustomerRequest, identityHasValidContractRequest.ContractKey);
                        return await Task.FromResult(new IdentityHasValidContractResponse { IsValid = false });
                    }

                    foreach (var contract in contracts.ContractInfoList)
                    {
                        if (contract.EndDate != null)
                        {
                            if (contract.EndDate.Value.Date.AddDays(1) > DateTime.Now)
                            {
                                if (contract.BillableObjectState == "User.GeneralPropertyItem.BillableObjectState_2" || contract.BillableObjectState == "تایید و شماره گذاری شده")
                                {
                                    return await Task.FromResult(new IdentityHasValidContractResponse { IsValid = true });
                                }
                                else
                                {
                                    Log.Debug("IdentityHasValidContract, SearchContract found but BillableObjectState is not 'User.GeneralPropertyItem.BillableObjectState_2' or 'تایید و شماره گذاری شده'. customerInfo:{@customerInfo}, contractKey:{@contractKey}", identityHasValidContractRequest.CustomerRequest, identityHasValidContractRequest.ContractKey);
                                }
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in IdentityHasValidContract. customerInfo: {@customerInfo}, contractKey: {@contractKey}", identityHasValidContractRequest.CustomerRequest, identityHasValidContractRequest.ContractKey);
            }

            Log.Debug("IdentityHasValidContract result is false. customerInfo:{@customerInfo}, contractKey:{@contractKey}", identityHasValidContractRequest.CustomerRequest, identityHasValidContractRequest.ContractKey);
            return await Task.FromResult(new IdentityHasValidContractResponse { IsValid = false });
        }
        public async Task<UserResponse> GetUserInfoByIdentityId(UserInfoByIdentityRequest userInfoByIdentityRequest)
        {
            try
            {
                using (var userChannel = CreateUserChannelClient())
                {
                    return await Task.FromResult(userChannel.GetUserByIdentityId(Username, Password, new Guid(userInfoByIdentityRequest.IdentityId)).ToDto());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in GetUserInfoByIdentityInfo. identityId: {identityId}", userInfoByIdentityRequest.IdentityId);
            }

            Log.Debug("GetUserInfoByIdentityInfo, GetUserByIdentityId result is null. {identityId}", userInfoByIdentityRequest.IdentityId);
            return null;
        }
        public async Task<UserTelephonySystemResponse> GetUserExtensions(UserExtensionsRequest userExtenstionsRequest)
        {
            Log.Debug($"GetUserExtensions. username:{userExtenstionsRequest.Username}");

            try
            {
                using (var userChannel = CreateUserChannelClient())
                {
                    return await Task.FromResult(userChannel.GetUserExtensions(Username, Password, userExtenstionsRequest.Username).ToDto());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetUserExtensions. username:{userExtenstionsRequest.Username}");
            }

            Log.Debug($"GetUserExtensions, GetUserExtensions result is null. {nameof(userExtenstionsRequest.Username)}:{userExtenstionsRequest.Username}");
            return null;
        }
        public async Task<UserExtensionResponse> GetUserDefaultExtension(UserExtensionRequest userExtensionRequest)
        {
            Log.Debug($"GetUserDefaultExtension. username:{userExtensionRequest.Username}, telephonySystemKey:{userExtensionRequest.TelephonySystemKey}");

            try
            {
                using (var userChannel = CreateUserChannelClient())
                {
                    var userTelephonySystemInfo = userChannel.GetUserExtensions(Username, Password, userExtensionRequest.Username);
                    if (userTelephonySystemInfo != null &&
                        userTelephonySystemInfo.TelephonySystems != null &&
                        userTelephonySystemInfo.TelephonySystems.Length > 0)
                    {
                        var telephonySystem = userTelephonySystemInfo.TelephonySystems.FirstOrDefault(x => x.Key == userExtensionRequest.TelephonySystemKey);
                        if (telephonySystem != null)
                        {
                            var extensions = telephonySystem.Extensions;
                            if (extensions != null && extensions.Length > 0)
                            {
                                return await Task.FromResult(new UserExtensionResponse { Extension = extensions.First().Name });
                            }
                            else
                            {
                                Log.Debug($"GetUserDefaultExtension, TelephonySystem extension is null or empty.");
                            }
                        }
                        else
                        {
                            Log.Debug($"GetUserDefaultExtension, TelephonySystem is not found with the given key.");
                        }
                    }
                    else
                    {
                        Log.Debug($"GetUserDefaultExtension, UserTelephonySystemInfo is null or empty.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetUserDefaultExtension, username:{userExtensionRequest.Username}, telephonySystemKey:{userExtensionRequest.TelephonySystemKey}");
            }

            Log.Debug($"GetUserExtenstion, GetUserDefaultExtension result is empty. {nameof(userExtensionRequest.Username)}:{userExtensionRequest.Username}, {nameof(userExtensionRequest.TelephonySystemKey)}:{userExtensionRequest.TelephonySystemKey}");
            return null;
        }
        public async Task<UserExtensionResponse> GetUserManagerExtension(UserManagerByExtensionRequest userManagerByExtensionRequest)
        {
            Log.Debug($"GetUserManagerExtension. {nameof(userManagerByExtensionRequest.TsId)}:{userManagerByExtensionRequest.TsId}, {nameof(userManagerByExtensionRequest.UserExtenstion)}:{userManagerByExtensionRequest.UserExtenstion}");

            try
            {
                using (var userChannel = CreateUserChannelClient())
                {
                    var result = userChannel.GetUserHelperExtensionBy(Username,
                                                                      Password,
                                                                      userManagerByExtensionRequest.UserExtenstion);
                    if (result.Success)
                        return await Task.FromResult(new UserExtensionResponse { Extension = result.HelperExtension });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"GetUserManagerExtension. {nameof(userManagerByExtensionRequest.UserExtenstion)}:{userManagerByExtensionRequest.UserExtenstion}");
            }

            Log.Debug($"GetUserManagerExtension result is null. {nameof(userManagerByExtensionRequest.TsId)}:{userManagerByExtensionRequest.TsId}, {nameof(userManagerByExtensionRequest.UserExtenstion)}:{userManagerByExtensionRequest.UserExtenstion}");
            return null;
        }
        public async Task<CustomerBalanceResponse> GetCustomerBalance(CustomerRequest customerRequest)
        {
            try
            {
                Guid customerId;
                if (Guid.TryParse(customerRequest.CustomerId, out customerId))
                {
                    using (var identityChannel = CreateIdentityChannelClient())
                    {
                        var identityInfoResult = identityChannel.FindIdentityById(Username, Password, new Guid(customerRequest.CustomerId));
                        if (identityInfoResult.Success &&
                            identityInfoResult.IdentityInfo != null &&
                            identityInfoResult.IdentityInfo.Balance.HasValue &&
                            identityInfoResult.IdentityInfo.Balance.Value < 0)
                        {
                            return await Task.FromResult(new CustomerBalanceResponse { Balance = Math.Abs(identityInfoResult.IdentityInfo.Balance.Value) });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in GetCustomerBalance. customerRequest: {@customerRequest}", customerRequest);
            }

            Log.Debug("GetCustomerBalance result is null. customerRequest:{@customerRequest}", customerRequest);
            return null;
        }
        public async Task<CardtableResponse> GetCardtable(CardtableRequest cardtableRequest)
        {
            Log.Debug($"GetCardtable. crmObjectTypeKey:{cardtableRequest.CrmObjectTypeKey}, identityId:{cardtableRequest.IdentityId}");

            try
            {
                using (var crmObjectTypeChannel = CreateCrmObjectTypeClient())
                {
                    return await Task.FromResult(crmObjectTypeChannel.GetCardtable(Username,
                                                             Password,
                                                             null,
                                                             null,
                                                             cardtableRequest.CrmObjectTypeKey,
                                                             new Guid(cardtableRequest.IdentityId),
                                                             SortOperator.Desc,
                                                             0,
                                                             1).ToDto());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"GetCardtable, crmObjectTypeKey:{cardtableRequest.CrmObjectTypeKey}, identityId:{cardtableRequest.IdentityId}");
            }

            return null;
        }
        public async Task<CreateInvoiceResponse> CreateInvoice(CreateSalesInvoiceRequest createSalesInvoiceRequest)
        {
            var result = MyIPgClient.GetSalesInvoiceClient().CallCreate(createSalesInvoiceRequest.ToSalesInvoiceCreateModel());
            return await Task.FromResult(new CreateInvoiceResponse
            {
                InvoiceId = result.CrmId
            });
        }
        public async Task<EncryptCrmObjectResponse> EncryptCrmObjectAsync(EncryptCrmObjectRequest encryptCrmObjectRequest)
        {
            var crmId = string.Empty;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(Host);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var formContent = new FormUrlEncodedContent(new[]
                {
                  new KeyValuePair<string, string>("grant_type", "password"),
                  new KeyValuePair<string, string>("username", Username),
                  new KeyValuePair<string, string>("password", Password),
                });

                HttpResponseMessage responseMessage = await client.PostAsync("/api/v2/auth/login", formContent);
                var responseJson = await responseMessage.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(responseJson);
                var token = jObj.GetValue("AccessToken").ToString();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage Res = await client.PostAsync("/api/v2/encryptor/encryptcrmobjectguid?id=" + encryptCrmObjectRequest.Guid, null);
                if (Res.IsSuccessStatusCode)
                {
                    var crmIdResponse = Res.Content.ReadAsStringAsync().Result;
                    crmId = JsonConvert.DeserializeObject<string>(crmIdResponse);
                }
            }
            return new EncryptCrmObjectResponse { EncryptedObject = crmId };
        }
        public async Task<BillableObjectTypePropsResponse> GetBillableObjectTypeProps(BillableObjectTypePropsRequest billableObjectTypePropsRequest)
        {
            try
            {
                using (var crmObjectType = CreateCrmObjectTypeClient())
                {
                    var crmObjectTypeInfo = crmObjectType.GetCrmObjectTypeInfo(Username, Password, billableObjectTypePropsRequest.BillableObjectTypeKey);
                    if (crmObjectTypeInfo != null &&
                        crmObjectTypeInfo.PropertyGroups != null &&
                        crmObjectTypeInfo.PropertyGroups.Length > 0 &&
                        crmObjectTypeInfo.PropertyGroups[0].Properties != null &&
                        crmObjectTypeInfo.PropertyGroups[0].Properties.Length > 0)
                    {
                        var propertyDisplayTypes = new[]
                        {
                    PropertyDisplayType.Text,
                    PropertyDisplayType.Currency,
                    PropertyDisplayType.Number
                };
                        var props = crmObjectTypeInfo.PropertyGroups[0].Properties.Where(x => propertyDisplayTypes.Contains(x.PropertyDisplayType.Value));
                        if (props != null)
                        {
                            return await Task.FromResult(new BillableObjectTypePropsResponse
                            {
                                CRMObjectTypes = props.Select(x => new CrmObjectTypeResponse
                                {
                                    Key = x.UserKey,
                                    Name = x.Name
                                }).ToList()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetBillableObjectTypeProps. billableObjectTypeKey:{billableObjectTypePropsRequest.BillableObjectTypeKey}");
            }

            return new BillableObjectTypePropsResponse();
        }
        public async Task<PaymentResponse> GetPaymentInfo(PaymentInfoRequest paymentInfoRequest)
        {
            try
            {
                using (var crmObjectType = CreateCrmObjectTypeClient())
                {
                    var crmObjectTypeInfo = crmObjectType.GetCrmObjectTypeInfo(Username, Password, paymentInfoRequest.BillableObjectTypeKey);
                    if (crmObjectTypeInfo != null)
                    {
                        Log.Debug("PayamgostarLookupSourceService GetPaymentInfo. CustomerRequest:{@CustomerRequest}, billableObjectTypeKey:{@billableObjectTypeKey}, billableObjectNumber:{@billableObjectNumber}, lookupNumberFieldKey:{@lookupNumberFieldKey}, valueFieldKey:{@valueFieldKey}, CrmObjectType:{@CrmObjectType}", paymentInfoRequest.CustomerRequest, paymentInfoRequest.BillableObjectTypeKey, paymentInfoRequest.BillableObjectNumber, paymentInfoRequest.LookupNumberFieldKey, paymentInfoRequest.ValueFieldKey, crmObjectTypeInfo.CrmObjectType);

                        switch (crmObjectTypeInfo.CrmObjectType)
                        {
                            case CrmObjectTypes.Invoice:
                                var invoiceResult = GetInvoiceInfo(paymentInfoRequest.CustomerRequest,
                                                      paymentInfoRequest.BillableObjectTypeKey,
                                                      paymentInfoRequest.BillableObjectNumber,
                                                      paymentInfoRequest.LookupNumberFieldKey,
                                                      paymentInfoRequest.ValueFieldKey);
                                return await Task.FromResult(new PaymentResponse { Amount = invoiceResult.Amount, IdentityId = invoiceResult.IdentityId });
                            case CrmObjectTypes.Quote:
                                break;
                            case CrmObjectTypes.Receipt:
                                break;
                            case CrmObjectTypes.Contract:
                                var contractResult = GetContractInfo(paymentInfoRequest.CustomerRequest,
                                                       paymentInfoRequest.BillableObjectTypeKey,
                                                       paymentInfoRequest.BillableObjectNumber,
                                                       paymentInfoRequest.LookupNumberFieldKey,
                                                       paymentInfoRequest.ValueFieldKey);
                                return await Task.FromResult(new PaymentResponse { Amount = contractResult.Amount, IdentityId = contractResult.IdentityId });
                            case CrmObjectTypes.PurchaseInvoice:
                                break;
                            case CrmObjectTypes.ReturnPurchaseInvoice:
                                break;
                            case CrmObjectTypes.ReturnSaleInvoice:
                                break;
                            case CrmObjectTypes.PurchaseQuote:
                                break;
                            case CrmObjectTypes.Payment:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in GetPaymentInfo, billableObjectNumber:{paymentInfoRequest.BillableObjectNumber}, billableObjectTypeKey:{paymentInfoRequest.BillableObjectTypeKey}, lookupNumberFieldKey:{paymentInfoRequest.LookupNumberFieldKey}");
            }

            return null;
        }
        public async Task<SendPaymentLinkToUserResponse> SendPaymentLinkToUser(SendPaymentLinkToUserRequest sendPaymentLinkToUserRequest)
        {
            SendPaymentLinkToUserResponse response = new SendPaymentLinkToUserResponse();
            response.Message = string.Empty;
            try
            {
                var paymentLinkInfo = new PaymentLinkInfo()
                {
                    IdentityId = new Guid(sendPaymentLinkToUserRequest.PaymentInfo.IdentityId),
                    Amount = sendPaymentLinkToUserRequest.PaymentInfo.Amount,
                    ExpireAfterDays = 7,
                    Description = string.Empty,
                    MoneyAccountUserKey = sendPaymentLinkToUserRequest.MoneyAccountUserKey,
                    MobilePhoneNumber = sendPaymentLinkToUserRequest.MobileNumber,
                    //PaymentTypeUserKey=
                };
                using (var epayChannel = CreateEpayClient())
                {
                    var paymentLinkInfoResult = epayChannel.CreatePaymentLink(Username, Password, paymentLinkInfo);
                    Log.Debug("SendPaymentLinkToUser, paymentLinkInfoResult:{@paymentLinkInfoResult}", paymentLinkInfoResult);
                    if (paymentLinkInfoResult.Success)
                    {
                        response.IsSuccess = true;
                        return await Task.FromResult(response);
                    }
                    else
                    {
                        response.IsSuccess = false;
                        response.Message = paymentLinkInfoResult.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                response.IsSuccess = false;
                response.Message = ex.Message;
                Log.Error(ex, "Error in SendPaymentLinkToUser. paymentInfo:{@paymentInfo}, mobileNumber:{@mobileNumber}, moneyAccountUserKey{@moneyAccountUserKey}", sendPaymentLinkToUserRequest.PaymentInfo, sendPaymentLinkToUserRequest.MobileNumber, sendPaymentLinkToUserRequest.MoneyAccountUserKey);
            }
            response.IsSuccess = false;
            return await Task.FromResult(response);
        }
        public Task<CrmObjectUrlResponse> GetCrmObjectUrl(CrmObjectUrlRequest crmObjectUrlRequest)
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {

        }

        public Task<SubmitQueueOperatorVotingResponse> SubmitQueueOperatorVoting(SubmitQueueOperatorVotingRequest submitQueueOperatorVotingRequest)
        {
            throw new NotImplementedException();
        }

        public Task<SubmitVotingResponse> SubmitVoting(SubmitVotingRequest submitVotingRequest)
        {
            throw new NotImplementedException();
        }
    }
}