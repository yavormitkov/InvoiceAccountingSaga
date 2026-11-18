using MediatR;
using Financial.Api.DTOs;
using System.Collections.Generic;

namespace Financial.Api.Queries;

public sealed record GetBailiffMonitorQuery : IRequest<IEnumerable<BailiffMonitorResponse>>;
