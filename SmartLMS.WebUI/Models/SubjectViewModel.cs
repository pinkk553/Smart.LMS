﻿using Carubbi.GenericRepository;
using SmartLMS.Domain.Entities.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SmartLMS.WebUI.Models
{
    public class SubjectViewModel
    {
        public bool Active { get; set; }
        public string KnowledgeAreaName { get; set; }

        public DateTime CreationDate { get; set; }

        [Display(Name = "Knowledge area")]
        [Required(ErrorMessage = "Select a knowledge area")]
        public Guid KnowledgeAreaId { get; set; }
        public Guid Id { get; set; }

        [Required]
        public int Order { get; set; }

        [Required]
        public string Name { get; set; }

        public IEnumerable<CourseViewModel> Courses { get; set; }

        internal static IEnumerable<SubjectViewModel> FromEntityList(IEnumerable<Subject> subjects, int depth)
        {
            return subjects.Select(item => FromEntity(item, depth));
        }

        internal static PagedListResult<SubjectViewModel> FromEntityList(PagedListResult<Subject> subjects)
        {
            return new PagedListResult<SubjectViewModel>
            {
                HasNext = subjects.HasNext,
                HasPrevious = subjects.HasPrevious,
                Count = subjects.Count,
                Entities = subjects.Entities.Select(item => FromEntity(item, 0)).ToList()
            };
        }


        internal static SubjectViewModel FromEntity(Subject item, int depth)
        {
            return new SubjectViewModel
            {
                Active = item.Active,
                KnowledgeAreaName = item.KnowledgeArea.Name,
                KnowledgeAreaId = item.KnowledgeArea.Id,
                CreationDate = item.CreatedAt,
                Id = item.Id,
                Order = item.Order,
                Name = item.Name,
                Courses = depth > 1
                ? CourseViewModel.FromEntityList(item.Courses.Where(x => x.Active).OrderBy(x => x.Order), depth)
                : new List<CourseViewModel>()
            };
        }

        internal static Subject ToEntity(SubjectViewModel subject, KnowledgeArea knowledgeArea)
        {
            return new Subject()
            {
                Id = subject.Id,
                KnowledgeArea = knowledgeArea,
                CreatedAt = subject.CreationDate,
                Active = subject.Active,
                Name = subject.Name,
                Order = subject.Order
            };
        }
    }
}