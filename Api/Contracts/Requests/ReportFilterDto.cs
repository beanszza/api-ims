using System;

namespace api_scm.Api.Contracts.Requests;

public class ReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
}
