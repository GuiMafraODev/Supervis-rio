
using System;
using System.Collections.Generic;

namespace SupervisorioECA.Bridge.Api
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }

        public ApiResponse()
        {
            Success = true;
            Message = "";
            Data = null;
            Timestamp = DateTime.Now;
        }

        public static ApiResponse Ok(object data)
        {
            ApiResponse response = new ApiResponse();

            response.Success = true;
            response.Message = "Operacao realizada.";
            response.Data = data;

            return response;
        }

        public static ApiResponse Ok(
            string message,
            object data)
        {
            ApiResponse response = new ApiResponse();

            response.Success = true;
            response.Message = message;
            response.Data = data;

            return response;
        }

        public static ApiResponse Error(string message)
        {
            ApiResponse response = new ApiResponse();

            response.Success = false;
            response.Message = message;
            response.Data = null;

            return response;
        }

        public static ApiResponse Error(
            string message,
            object data)
        {
            ApiResponse response = new ApiResponse();

            response.Success = false;
            response.Message = message;
            response.Data = data;

            return response;
        }
    }

    public class ApiTagValue
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double? Value { get; set; }
        public string Unit { get; set; }
        public string Quality { get; set; }
        public DateTime? Timestamp { get; set; }
        public bool Available { get; set; }
        public string Error { get; set; }

        public ApiTagValue()
        {
            Id = "";
            Name = "";
            Unit = "";
            Quality = "Bad";
            Available = false;
            Error = "";
        }
    }

    public class ApiCommunicationStatus
    {
        public bool ApiOnline { get; set; }
        public bool OpcConnected { get; set; }
        public string OpcServer { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }

        public ApiCommunicationStatus()
        {
            ApiOnline = true;
            OpcConnected = false;
            OpcServer = "";
            Message = "Aguardando comunicacao OPC.";
            Timestamp = DateTime.Now;
        }
    }


    public class ApiAlarm
    {
        public long Id { get; set; }

        public string TagId { get; set; }
        public string TagName { get; set; }
        public string AlarmType { get; set; }
        public string State { get; set; }

        public double Value { get; set; }
        public double AlarmLimit { get; set; }

        public DateTime TriggeredAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? NormalizedAt { get; set; }

        public string AcknowledgedBy { get; set; }
        public string Description { get; set; }

        public ApiAlarm()
        {
            Id = 0;

            TagId = "";
            TagName = "";
            AlarmType = "";
            State = "";

            Value = 0;
            AlarmLimit = 0;

            TriggeredAt = DateTime.MinValue;
            AcknowledgedAt = null;
            NormalizedAt = null;

            AcknowledgedBy = "";
            Description = "";
        }
    }


    public class ApiTrendPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Quality { get; set; }
    }

    public class ApiEvent
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string TagId { get; set; }
        public string TagName { get; set; }
        public string AlarmType { get; set; }
        public string Description { get; set; }
        public double? Value { get; set; }
        public double? AlarmLimit { get; set; }
        public string User { get; set; }
        public double? PreviousLimit { get; set; }
        public double? NewLimit { get; set; }
        public string State { get; set; }
    }
}
