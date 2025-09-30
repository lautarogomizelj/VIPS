namespace VIPS.Web.Models
{
    public class VroomResult
    {
        public int Code { get; set; }
        public VroomSummary Summary { get; set; }
        public List<VroomJob> Unassigned { get; set; }
        public List<VroomRoute> Routes { get; set; }
    }

    public class VroomRoute
    {
        public int Vehicle { get; set; }
        public List<VroomStep> Steps { get; set; }
        public int Cost { get; set; }
        public List<int> Delivery { get; set; }
        public List<int> Amount { get; set; }
        public List<int> Pickup { get; set; }
        public int Setup { get; set; }
        public int Service { get; set; }
        public int Duration { get; set; }
        public int WaitingTime { get; set; }
        public int Priority { get; set; }
        public List<object> Violations { get; set; }
    }

    public class VroomStep
    {
        public string Type { get; set; } // "start", "job", "end"
        public int Id { get; set; }      // id del pedido
        public int Job { get; set; }     // solo para pasos tipo "job"
        public List<int> Load { get; set; }
        public int Arrival { get; set; }
        public int Duration { get; set; }
        public int Setup { get; set; }
        public int Service { get; set; }
        public int WaitingTime { get; set; }
        public List<object> Violations { get; set; }
    }

    public class VroomSummary
    {
        public int Cost { get; set; }
        public int Routes { get; set; }
        public int Unassigned { get; set; }
        public List<int> Delivery { get; set; }
        public List<int> Amount { get; set; }
        public List<int> Pickup { get; set; }
        public int Setup { get; set; }
        public int Service { get; set; }
        public int Duration { get; set; }
        public int WaitingTime { get; set; }
        public int Priority { get; set; }
        public List<object> Violations { get; set; }
        public VroomComputingTimes ComputingTimes { get; set; }
    }

    public class VroomComputingTimes
    {
        public int Loading { get; set; }
        public int Solving { get; set; }
        public int Routing { get; set; }
    }

    public class VroomJob
    {
        public int Id { get; set; }
        public int[] Amount { get; set; }
        // podés agregar otras propiedades si VROOM las devuelve
    }
}
