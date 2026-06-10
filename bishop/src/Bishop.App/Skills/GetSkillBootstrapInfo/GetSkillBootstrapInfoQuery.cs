using Bishop.Core;
using MediatR;

namespace Bishop.App.Skills.GetSkillBootstrapInfo;

public sealed record GetSkillBootstrapInfoQuery(Guid WorkspaceId) : IRequest<SkillBootstrapInfo>;
