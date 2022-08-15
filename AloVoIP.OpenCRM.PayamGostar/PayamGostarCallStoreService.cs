using AloVoIP.OpenCRM.Enums;
using AloVoIP.OpenCRM.Requests;
using AloVoIP.OpenCRM.Responses;
using PayamGostarClient;
using PayamGostarClient.TelephonySystem;
using Serilog;
using System;
using System.Threading.Tasks;
using ChannelResponse = AloVoIP.OpenCRM.Enums.ChannelResponse;

namespace AloVoIP.OpenCRM.PayamGostar
{
    public class PayamGostarCallStoreService : ICallStoreService
    {
        private IPgClient _pgClient;
        private DateTime _nextClientCreateDate;

        public virtual string CallStoreId { get; }
        protected string Host { get; }
        protected string Username { get; }
        protected string Password { get; }

        public PayamGostarCallStoreService(string callStoreId, string host, string username, string password)
        {
            CallStoreId = callStoreId;
            Host = host;
            Username = username;
            Password = password;
        }
        protected IPgClient MyIPgClient
        {
            get
            {
                if (_pgClient == null || _nextClientCreateDate < DateTime.Now)
                {
                    _pgClient = Create(Host, Username, Password);
                    _nextClientCreateDate = DateTime.Now.AddHours(1);
                }

                return _pgClient;
            }
        }
        private IPgClient Create(string endPointAddress, string userName, string password)
        {
            return new PgClientFactory().Create(endPointAddress, new PgCredentials()
            {
                Username = userName,
                Password = password
            });
        }

        private PhoneCallType ConvertToPgCallType(CallType phoneCallType, CallResult callResult)
        {
            switch (phoneCallType)
            {
                case CallType.Incoming:
                    return callResult == CallResult.Answered
                            ? PhoneCallType.ReceivedCall
                            : PhoneCallType.MissedCall;
                case CallType.Outgoing:
                    return PhoneCallType.OutgoingCall;
                case CallType.Internal:
                    return PhoneCallType.Internal;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private TelephonySystemPeerType ConvertToPgPeerType(PeerType channelOwnerType)
        {
            switch (channelOwnerType)
            {
                case PeerType.Trunk:
                    return TelephonySystemPeerType.Trunk;
                case PeerType.Extension:
                    return TelephonySystemPeerType.Extension;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private PayamGostarClient.TelephonySystem.ChannelResponse ConvertToPgChannelResponseType(ChannelResponse channelResponse)
        {
            switch (channelResponse)
            {
                case ChannelResponse.Answered:
                    return PayamGostarClient.TelephonySystem.ChannelResponse.Answered;
                case ChannelResponse.NotAnswered:
                    return PayamGostarClient.TelephonySystem.ChannelResponse.NotAnswered;
                case ChannelResponse.Busy:
                    return PayamGostarClient.TelephonySystem.ChannelResponse.Busy;
                case ChannelResponse.Transfered:
                    return PayamGostarClient.TelephonySystem.ChannelResponse.Transfered;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private ChannelStatus ConvertToPgChannelStatusType(ChannelState channelState)
        {
            switch (channelState)
            {
                case ChannelState.Down:
                case ChannelState.OffHook:
                case ChannelState.Unknown:
                case ChannelState.Busy:
                    return ChannelStatus.Down;
                case ChannelState.PreRing:
                case ChannelState.Ring:
                case ChannelState.Ringing:
                case ChannelState.Dialing:
                case ChannelState.DialingOffhook:
                    return ChannelStatus.Ringing;
                case ChannelState.Up:
                    return ChannelStatus.Up;
                case ChannelState.Hangedup:
                    return ChannelStatus.HangUp;
                default:
                    Log.Error($"Error in converting channelStatus to PayamGostarChannelStatus: {nameof(channelState)}:{channelState}");
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Task<CallCreateResponse> CallCreated(CallCreateRequest callCreateRequest)
        {
            var callCreateResult = MyIPgClient.GetTelephonySystem().CallCreate(new CallCreateModel()
            {
                TsKey = callCreateRequest.TsKey,
                SourceId = callCreateRequest.SourceCallId,
                PhoneNumber = callCreateRequest.Number,
                StartDate = callCreateRequest.Date,
                CallTypeIndex = ConvertToPgCallType(callCreateRequest.CallType, callCreateRequest.CallResult),
                InitChannelSourceId = callCreateRequest.SourceInitCallChannelId,
                InitChannelPeerName = callCreateRequest.SourceInitCallChannelPeerName,
                InitChannelPeerTypeIndex = ConvertToPgPeerType(callCreateRequest.SourceInitCallChannelPeerType),
                IsLive = callCreateRequest.IsLive
            });
            CallCreateResponse response = new CallCreateResponse()
            {
                CallId = callCreateResult.CallId.ToString(),
                InitCallChannelId = callCreateResult.InitChannelId.ToString(),
                IdentityId = callCreateResult.IdentityId.HasValue ? callCreateResult.IdentityId.ToString() : null,
                IdentityName = callCreateResult.IdentityNickName,
            };
            return Task.FromResult(response);
        }
        public Task<CallUpdateResponse> CallUpdated(CallUpdateRequest callUpdateRequest)
        {
            var model = new CallUpdateModel()
            {
                CallId = long.Parse(callUpdateRequest.CallId),
                PhoneNumber = callUpdateRequest.Number,
                EndDate = callUpdateRequest.Date,
                CallTypeIndex = ConvertToPgCallType(callUpdateRequest.CallType, callUpdateRequest.CallResult),
                IsLive = callUpdateRequest.IsLive
            };
            if (!string.IsNullOrEmpty(callUpdateRequest.IdentityId))
                model.IdentityId = new Guid(callUpdateRequest.IdentityId);

            var callupdateResult = MyIPgClient.GetTelephonySystem().CallUpdate(model);
            CallUpdateResponse response = new CallUpdateResponse()
            {
                IdentityId = callupdateResult.IdentityId.HasValue ? callupdateResult.IdentityId.ToString() : null,
                IdentityName = callupdateResult.IdentityNickName,
            };
            return Task.FromResult(response);
        }
        public Task<CallChannelCreateResponse> CallChannelCreated(CallChannelCreateRequest callChannelCreateRequest)
        {
            var callChannelCreateResult = MyIPgClient.GetTelephonySystem().CallChannelCreate(new CallChannelCreateModel()
            {
                CallId = long.Parse(callChannelCreateRequest.CallId),
                ChannelPeerName = callChannelCreateRequest.PeerName,
                ChannelPeerTypeIndex = ConvertToPgPeerType(callChannelCreateRequest.PeerType),
                ChannelSourceId = callChannelCreateRequest.SourceCallChannelId,
                ChannelStatusIndex = ConvertToPgChannelStatusType(callChannelCreateRequest.ChannelState),
                CreateDate = callChannelCreateRequest.CreateDate,
                IsLive = callChannelCreateRequest.IsLive
            });

            CallChannelCreateResponse response = new CallChannelCreateResponse()
            {
                CallChannelId = callChannelCreateResult.CallChannelId.ToString()
            };
            return Task.FromResult(response);
        }
        public Task<CallChannelUpdateResponse> CallChannelUpdated(CallChannelUpdateRequest callChannelUpdateRequest)
        {
            MyIPgClient.GetTelephonySystem().CallChannelUpdate(new CallChannelUpdateModel()
            {
                CallChannelId = long.Parse(callChannelUpdateRequest.ChannelId),
                ChannelStatusIndex = ConvertToPgChannelStatusType(callChannelUpdateRequest.ChannelState),
                ChannelResponseIndex = ConvertToPgChannelResponseType(callChannelUpdateRequest.ChannelResponse),
                ConnectDate = callChannelUpdateRequest.ConnectDate,
                HangupDate = callChannelUpdateRequest.HangupDate,
                RecordedFileName = callChannelUpdateRequest.RecordedFileName,
                IsLive = callChannelUpdateRequest.IsLive,
                ToChangePeerName = callChannelUpdateRequest.ToChangePeerName,
                ToChangePeerTypeIndex = callChannelUpdateRequest.ToChangePeerType != null ?
                                            ConvertToPgPeerType(callChannelUpdateRequest.ToChangePeerType.Value) :
                                            (TelephonySystemPeerType?)null
            });

            CallChannelUpdateResponse response = new CallChannelUpdateResponse()
            {
                CallChannelId = callChannelUpdateRequest.ChannelId.ToString()
            };
            return Task.FromResult(response);
        }
        public Task<MergeCallResponse> MergeCall(MergeCallRequest mergeCallRequest)
        {
            MyIPgClient.GetTelephonySystem().MergeCall(new Septa.PayamGostarApiClient.TelephonySystem.CallMergeModel()
            {
                TsKey = mergeCallRequest.TsKey,
                SourceCallId = mergeCallRequest.SourceCallId,
                DestinationCallId = mergeCallRequest.DestCallId,
            });
            MergeCallResponse response = new MergeCallResponse()
            {
                Merged = true
            };
            return Task.FromResult(response);
        }
    }
}
