namespace SpeedControlSystem.Exceptions
{
    public class CustomRpcException : Exception
    {
        private readonly Status status;
        public CustomRpcException(Status status)
        {
            this.status = status;
        }

        public Status Status => status;


    }
}