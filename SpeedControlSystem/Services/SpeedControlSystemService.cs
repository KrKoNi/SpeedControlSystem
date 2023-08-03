using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using SpeedControlSystem.Exceptions;
namespace SpeedControlSystem.Services;

public class SpeedControlSystemService : SpeedRecorder.SpeedRecorderBase
{
    private readonly IConfiguration _configuration;

    public SpeedControlSystemService(IConfiguration configuration)
    {
        _configuration = configuration;

    }
    
    void ValidateWorkTime()
    {
        var currentHour = DateTime.Now.Hour;
        var workStartHour = int.Parse(_configuration["hoursWorkStart"]);
        var workEndHour = int.Parse(_configuration["hoursWorkEnd"]);

        if (currentHour < workStartHour || currentHour > workEndHour)
        {
            throw new CustomRpcException(new Status
            {
                Code = (int)StatusCode.PermissionDenied,
                Message = $"Service works from {workStartHour} to {workEndHour}",
                Details = { Any.Pack(new Empty()) }
            });
        }
    }
    
    public override async Task<Status> RecordSpeed(SpeedInfo request, ServerCallContext context)
    {
        string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), request.RecordTime.ToDateTime().Date.ToShortDateString() + ".txt");

        await using var output = File.Open(filePath, FileMode.Append);
        request.WriteDelimitedTo(output);
        
        return new Status
        {
            Code = (int)StatusCode.OK,
            Message = $"The information was recorder to {filePath}",
            Details = { Any.Pack(new Empty()) }
        };
    }

    
    public override async Task<Status> GetSpeedInfoAboveThreshold(DateSpeedThreshold request, ServerCallContext context)
    {
        try
        {
            ValidateWorkTime();
        }
        catch (CustomRpcException e)
        {
            return e.Status;
        }

        string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), request.RecordDate.ToDateTime().Date.ToShortDateString() + ".txt");
        var speedInfoList = new SpeedInfoList();

        if (!File.Exists(filePath))
        {
            return new Status
            {
                Code = (int)StatusCode.Aborted,
                Message = $"File {filePath} doesn't exist",
                Details = { Any.Pack(new Empty()) }
            };
        }
        
        await using var input = File.OpenRead(filePath);
        
        while (input.Position < input.Length)
        {
            var speedInfo = SpeedInfo.Parser.ParseDelimitedFrom(input);
            if (speedInfo.RecordedSpeed > request.ThresholdSpeed)
            {
                speedInfoList.SpeedInfo.Add(speedInfo);
            }
        }

        return new Status
        {
            Code = (int)StatusCode.OK,
            Message = $"",
            Details = { Any.Pack(speedInfoList) }
        };
    }

    public override async Task<Status> GetMinMaxSpeedInfo(DateOnly request, ServerCallContext context)
    {
        try
        {
            ValidateWorkTime();
        }
        catch (CustomRpcException e)
        {
            return e.Status;
        }

        string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), request.RecordDate.ToDateTime().Date.ToShortDateString() + ".txt");
        var speedInfoList = new SpeedInfoList();
        
        if (!File.Exists(filePath))
        {
            return new Status
            {
                Code = (int)StatusCode.Aborted,
                Message = $"File {filePath} doesn't exist",
                Details = { Any.Pack(new Empty()) }
            };
        }
        
        await using var input = File.OpenRead(filePath);
        
        SpeedInfo minSpeedInfo = new SpeedInfo();
        SpeedInfo maxSpeedInfo = new SpeedInfo();

        double minSpeed = Double.PositiveInfinity;
        double maxSpeed = 0;
        while (input.Position < input.Length)
        {
            var speedInfo = SpeedInfo.Parser.ParseDelimitedFrom(input);
            if (speedInfo.RecordedSpeed > maxSpeed)
            {
                maxSpeed = speedInfo.RecordedSpeed;
                maxSpeedInfo = speedInfo;
            }
            if (speedInfo.RecordedSpeed < minSpeed)
            {
                minSpeed = speedInfo.RecordedSpeed;
                minSpeedInfo = speedInfo;
            }
        }
        
        speedInfoList.SpeedInfo.Add(minSpeedInfo);
        speedInfoList.SpeedInfo.Add(maxSpeedInfo);
        
        return new Status
        {
            Code = (int)StatusCode.OK,
            Message = $"",
            Details = { Any.Pack(speedInfoList) }
        };
    }
   
}