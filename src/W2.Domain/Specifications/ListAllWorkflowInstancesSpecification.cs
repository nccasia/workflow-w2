﻿using Elsa.Models;
using Elsa.Persistence.Specifications;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace W2.Specifications
{
    public class ListAllWorkflowInstancesSpecification : Specification<WorkflowInstance>
    {
        public string TenantId { get; private set; }
        public string[] Ids { get; set; }

        public ListAllWorkflowInstancesSpecification(string tenantId, string[] ids)
        {
            TenantId = tenantId;
            Ids = ids;
        }

        public override Expression<Func<WorkflowInstance, bool>> ToExpression()
        {
            Expression<Func<WorkflowInstance, bool>> predicate = x => true;

            if (Ids != null && Ids.Any())
            {
                predicate = predicate.And(x => Ids.Contains(x.Id));
            }

            if (!string.IsNullOrWhiteSpace(TenantId))
            {
                predicate = predicate.And(x => x.TenantId == TenantId);
            }

            return predicate;
        }
    }
}
