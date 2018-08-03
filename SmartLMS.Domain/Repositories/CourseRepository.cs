﻿using Carubbi.GenericRepository;
using SmartLMS.Domain.Entities.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartLMS.Domain.Repositories
{
    public class CourseRepository
    {

        private readonly IContext _context;
        public CourseRepository(IContext context)
        {
            _context = context;
        }

        public List<Course> ListActiveCourses()
        {
            return _context.GetList<Course>()
                .Where(a => a.Active)
                .OrderBy(a => a.Name)
                .ToList();
        }


        public CourseIndex GetCourseIndex(Guid id, Guid? userId)
        {
            var courseIndex = new CourseIndex();
            var classRepository = new ClassRepository(_context);
            

            courseIndex.Course = _context.GetList<Course>().Find(id);
            courseIndex.ClassesInfo = courseIndex.Course.Classes.Where(a => a.Active)
                .OrderBy(x => x.Order)
                .Select(a => new ClassInfo {
                    Class = a,
                    Available = userId.HasValue && classRepository.CheckClassAvailability(a.Id, userId.Value),
                    Percentual = userId.HasValue ? a.Accesses.LastOrDefault(x => x.User.Id == userId)?.Percentual ?? 0 : 0
            });

            return courseIndex;
            
        }

        public PagedListResult<Course> Search(string term, string searchFieldName, int page)
        {
            var repo = new GenericRepository<Course>(_context);
            var query = new SearchQuery<Course>();
            query.AddFilter(a => (searchFieldName == "Name" && a.Name.Contains(term)) ||
                                 (searchFieldName == "Id" && a.Id.ToString().Contains(term)) ||
                                  (searchFieldName == "Knowledge Area" && a.Subject.KnowledgeArea.Name.Contains(term)) ||
                                  (searchFieldName == "Subject" && a.Subject.Name.Contains(term)) ||
                                    string.IsNullOrEmpty(searchFieldName));

            query.AddSortCriteria(new DynamicFieldSortCriteria<Course>("Subject.KnowledgeArea.Order, Subject.Order, Order"));

            query.Take = 8;
            query.Skip = ((page - 1) * 8);

            return repo.Search(query);
        }

        public void Delete(Guid id)
        {
            var course = GetById(id);
            _context.GetList<Course>().Remove(course);
           
        }

        public Course GetById(Guid id)
        {
            return _context.GetList<Course>().Find(id);
        }

        public void Create(Course course)
        {
            course.CreatedAt = DateTime.Now;
            course.Active = true;
            _context.GetList<Course>().Add(course);
         
        }

        public void Update(Course course)
        {
            var currentCourse = GetById(course.Id);
            _context.Update(currentCourse, course);
          
        }

        public Course GetByImageName(string imageName)
        {
            return _context
                .GetList<Course>()
                .FirstOrDefault(x => x.Image == imageName);
        }

        public void UpdateWithClasses(Course course)
        {
            var currentCourse = GetById(course.Id);
            course.CreatedAt = currentCourse.CreatedAt;
            currentCourse.Subject = course.Subject;
            currentCourse.TeacherInCharge = course.TeacherInCharge;
            course.Classes = currentCourse.Classes;
            _context.Update(currentCourse, course);

            if (course.Active) return;
            var classRepository = new ClassRepository(_context);
            foreach (var klass in course.Classes)
            {
                klass.Active = false;
                classRepository.Update(klass);
            }

        }

   
    }
}
