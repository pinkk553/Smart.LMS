﻿using Carubbi.Utils.Data;
using Humanizer.DateTimeHumanizeStrategy;
using SmartLMS.Domain;
using SmartLMS.Domain.Entities;
using SmartLMS.Domain.Entities.Content;
using SmartLMS.Domain.Entities.History;
using SmartLMS.Domain.Repositories;
using SmartLMS.WebUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Transactions;
using System.Web.Mvc;
using SmartLMS.Domain.Entities.UserAccess;

namespace SmartLMS.WebUI.Controllers
{
    [Authorize]
    public class ClassController : BaseController
    {
        public ClassController(IContext contexto)
            : base(contexto)
        {

        }

        [HttpPost]
        public ActionResult UpdateProgress(ClassAccessViewModel viewModel)
        {
            var classAccessRepository = new ClassAccessRepository(_context);
            var classRepository = new ClassRepository(_context);
            var classInfo = classRepository.GetClass(viewModel.ClassId, _loggedUser.Id);
            classAccessRepository.UpdateProgress(viewModel.ToEntity(_loggedUser, classInfo.Class));

            return new HttpStatusCodeResult(HttpStatusCode.OK, "Updated");
        }

        public ActionResult Watch(Guid id)
        {
            var classRepository = new ClassRepository(_context);
            var classAccessRepository = new ClassAccessRepository(_context);
            var classInfo = classRepository.GetClass(id, _loggedUser.Id);

            if (classInfo.Available && classInfo.Class.Active)
            {
                classAccessRepository.CreateAccess(new ClassAccess()
                {
                    Class = classInfo.Class,
                    User = _loggedUser,
                    AccessDateTime = DateTime.Now,
                    Percentual = classInfo.Percentual,
                    WatchedSeconds = classInfo.WatchedSeconds
                });

                return View(ClassViewModel.FromEntityWithFiles(classInfo));
            }

            TempData["MessageType"] = "warning";
            TempData["MessageTitle"] = "Warning";
            TempData["Message"] = "You are not allowed to watch this class";
            return RedirectToAction("Index", "Home");

        }

        [AllowAnonymous]
        public ActionResult Index(Guid id)
        {
            var courseRepository = new CourseRepository(_context);
            var courseIndex = courseRepository.GetCourseIndex(id, _loggedUser?.Id);

            if (!courseIndex.Course.Active)
            {
                TempData["MessageType"] = "warning";
                TempData["MessageTitle"] = "Warning";
                TempData["Message"] = "This course is not available at this moment";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = CourseViewModel.FromEntity(courseIndex);

            ViewBag.OtherCourses = new SelectList(courseIndex.Course.Subject.Courses
                .Where(c => c.Active)
                .Except(new List<Course> { courseIndex.Course }), "Id", "Name");

            return View(viewModel);
        }

        [AllowAnonymous]
        public ActionResult CoursePanel(Guid id)
        {
            var courseRepository = new CourseRepository(_context);
            var courseIndex = courseRepository.GetCourseIndex(id, _loggedUser?.Id);
            var viewModel = CourseViewModel.FromEntity(courseIndex);
            return PartialView("_CoursePanel", viewModel);
        }

        [ChildActionOnly]
        public ActionResult ClassListSmall(Guid id, Guid currentClassId)
        {
            var courseRepository = new CourseRepository(_context);
            var courseIndex = courseRepository.GetCourseIndex(id, _loggedUser.Id);
            var viewModel = CourseViewModel.FromEntity(courseIndex);
            ViewBag.CurrentClassId = currentClassId;
            return PartialView("_ClassListSmall", viewModel.Classes);
        }


        [ChildActionOnly]
        public ActionResult NewClassesPanel()
        {
            var classRepository = new ClassRepository(_context);
            return PartialView("_NewClassesPanel", ClassViewModel.FromEntityList(classRepository.ListLastDeliveredClasses(_loggedUser.Id), new DefaultDateTimeHumanizeStrategy()));
        }

        [ChildActionOnly]
        public ActionResult LastClassesPanel()
        {
            var classAccessRepository = new ClassAccessRepository(_context);
            return PartialView("_LastClassesPanel", ClassAccessViewModel.FromEntityList(classAccessRepository.ListLastAccesses(_loggedUser.Id), new DefaultDateTimeHumanizeStrategy()));
        }

        [HttpPost]
        public ActionResult ListComments(Guid classId, int page = 1)
        {
            var classRepository = new ClassRepository(_context);
            var classInfo = classRepository.GetClass(classId, _loggedUser.Id);
            var humanizer = new DefaultDateTimeHumanizeStrategy();
            var comments = classInfo.Class.Comments
                .OrderByDescending(x => x.DateTime)
                .Skip(((page - 1) * 10))
                .Take(10)
                .ToList();

            return Json(CommentViewModel.FromEntityList(comments, humanizer, _loggedUser.Id), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Comment(FormCollection formData)
        {
            if (string.IsNullOrEmpty(formData["Comment"])) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);


            var comment = new CommentViewModel
            {
                ClassId = new Guid(formData["ClassId"]),
                CommentText = formData["CommentText"]
            };

            var classRepository = new ClassRepository(_context);
            var classInfo = classRepository.GetClass(comment.ClassId, _loggedUser.Id);
            comment.DateTime = DateTime.Now;
            classRepository.SendComment(comment.ToEntity(_loggedUser, classInfo.Class));
            _context.Save(_loggedUser);

            return new HttpStatusCodeResult(HttpStatusCode.OK);

        }

        [HttpPost]

        public ActionResult DeleteComment(long commentId)
        {
            var classRepository = new ClassRepository(_context);
            classRepository.DeleteComment(commentId);
            _context.Save(_loggedUser);
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [AllowAnonymous]
        public ActionResult Download(Guid id)
        {

            var classRepository = new ClassRepository(_context);
            var file = classRepository.GetFile(id);

            var classAvailable = classRepository.CheckClassAvailability(file.Class.Id, _loggedUser?.Id);
            if (classAvailable)
            {
                classRepository.SaveAccess(file, _loggedUser);
                return File(Url.Content("~/" + Parameter.FILE_STORAGE + "/" + file.Class.Id + "/" + file.PhysicalPath), "application/octet-stream", file.PhysicalPath);
            }
            else
            {
                TempData["MessageType"] = "error";
                TempData["MessageTitle"] = "Support material download";
                TempData["Message"] = "You don't have permission to download this material";
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize(Roles = "Admin")]
        public ActionResult IndexAdmin(string term, string searchFieldName, int page = 1)
        {
            ViewBag.SearchFields = new SelectList(new string[] { "Name", "Subject", "Knowledge Area", "Course", "Id" });
            var classRepository = new ClassRepository(_context);
            return View(ClassViewModel.FromEntityList(classRepository.Search(term, searchFieldName, page)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult Search(string term, string searchFieldName, int page = 1)
        {
            var classRepository = new ClassRepository(_context);
            return Json(ClassViewModel.FromEntityList(classRepository.Search(term, searchFieldName, page)));
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Create()
        {
            var courseRepository = new CourseRepository(_context);
            var activeCourses = courseRepository.ListActiveCourses();
            ViewBag.Courses = new SelectList(activeCourses, "Id", "Name");

            var userRepository = new UserRepository(_context);
            var activeTeachers = userRepository.ListActiveTeachers();
            ViewBag.Teachers = new SelectList(activeTeachers, "Id", "Name");
            const ContentType contentType = ContentType.Vimeo;

            ViewBag.ContentTypes = new SelectList(contentType.ToDataSource<ContentType>(), "Key", "Value");

            return View();
        }

        // POST: teacher/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult Create(ClassViewModel viewModel)
        {
            var courseRepository = new CourseRepository(_context);
            var userRepository = new UserRepository(_context);

            if (ModelState.IsValid)
            {
                try
                {
                    var course = courseRepository.GetById(viewModel.CourseId);
                    var teacher = (Teacher)userRepository.GetById(viewModel.TeacherId);
                    var classRepository = new ClassRepository(_context);
                    classRepository.Create(ClassViewModel.ToEntity(viewModel, course, teacher));
                    _context.Save(_loggedUser);
                    TempData["MessageType"] = "success";
                    TempData["MessageTitle"] = "Content management";
                    TempData["Message"] = "Class created";
                    return RedirectToAction("IndexAdmin");
                }
                catch (Exception ex)
                {
                    TempData["MessageType"] = "error";
                    TempData["MessageTitle"] = "Content management";
                    TempData["Message"] = ex.Message;
                }
            }

            const ContentType classType = ContentType.Vimeo;
            ViewBag.ContentTypes = new SelectList(classType.ToDataSource<ContentType>(), "Key", "Value");

            var activeCourses = courseRepository.ListActiveCourses();
            ViewBag.Courses = new SelectList(activeCourses, "Id", "Name");
            var activeTeachers = userRepository.ListActiveTeachers();
            ViewBag.Teachers = new SelectList(activeTeachers, "Id", "Name");
            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult SaveSupportMaterial(string id)
        {
            using (var tx = new TransactionScope())
            {
                var uploader = new SupportMaterialUploader(id);
                var uploadResult = uploader.Upload(Request.Files[0]);

                var classRepository = new ClassRepository(_context);
                var klass = classRepository.GetById(new Guid(id));

                klass.Files.Add(new File()
                {
                    PhysicalPath = uploadResult.Message,
                    CreatedAt = DateTime.Now,
                    Active = true,
                    Name = Request.Files[0]?.FileName,
                    Class = klass
                });

                _context.Save(_loggedUser);

                tx.Complete();
                return Json(uploadResult);
            }
        }

        [Authorize(Roles = "Admin")]
        public ActionResult DeleteSupportMaterial(string id, string fileName)
        {
            using (var tx = new TransactionScope())
            {
                var classRepository = new ClassRepository(_context);
                var klass = classRepository.GetById(new Guid(id));
                var file = klass.Files.Single(x => x.PhysicalPath == fileName);
                klass.Files.Remove(file);
                classRepository.UpdateWithFiles(klass);
                _context.Save(_loggedUser);

                var uploader = new SupportMaterialUploader(id);
                uploader.DeleteFile(fileName);

                tx.Complete();
            }
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        public ActionResult ListSupportMaterial(string classId)
        {
            var classRepository = new ClassRepository(_context);
            var klass = classRepository.GetById(new Guid(classId));

            var files = new List<dynamic>();
            var uploader = new SupportMaterialUploader(classId);

            foreach (var item in klass.Files)
            {
                files.Add(new
                {
                    item.Name,
                    Size = uploader.GetFileInfo(item.PhysicalPath).Length
                });
            }

            return Json(new { Files = files });
        }


        [Authorize(Roles = "Admin")]
        public ActionResult Edit(Guid id)
        {
            var courseRepository = new CourseRepository(_context);
            var activeCourses = courseRepository.ListActiveCourses();
            ViewBag.Courses = new SelectList(activeCourses, "Id", "Name");

            var userRepository = new UserRepository(_context);
            var activeTeachers = userRepository.ListActiveTeachers();
            ViewBag.Teachers = new SelectList(activeTeachers, "Id", "Name");
            const ContentType contentType = ContentType.Vimeo;

            ViewBag.ContentTypes = new SelectList(contentType.ToDataSource<ContentType>(), "Key", "Value");


            var classRepository = new ClassRepository(_context);
            var klass = classRepository.GetById(id);
            return View(ClassViewModel.FromEntity(klass, 0, new DefaultDateTimeHumanizeStrategy()));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public ActionResult Edit(Guid id, ClassViewModel viewModel)
        {
            var courseRepository = new CourseRepository(_context);
            var userRepository = new UserRepository(_context);

            if (ModelState.IsValid)
            {
                try
                {
                    var course = courseRepository.GetById(viewModel.CourseId);
                    var teacher = (Teacher)userRepository.GetById(viewModel.TeacherId);
                    var classRepository = new ClassRepository(_context);
                    classRepository.Update(ClassViewModel.ToEntity(viewModel, course, teacher));
                    _context.Save(_loggedUser);
                    TempData["MessageType"] = "success";
                    TempData["MessageTitle"] = "Content management";
                    TempData["Message"] = "Class updated";
                    return RedirectToAction("IndexAdmin");
                }
                catch (Exception ex)
                {
                    TempData["MessageType"] = "error";
                    TempData["MessageTitle"] = "Content management";
                    TempData["Message"] = ex.Message;
                }
            }

            var activeCourses = courseRepository.ListActiveCourses();
            ViewBag.Courses = new SelectList(activeCourses, "Id", "Name");

            var activeTeachers = userRepository.ListActiveTeachers();
            ViewBag.Teachers = new SelectList(activeTeachers, "Id", "Name");
            const ContentType contetTypes = ContentType.Vimeo;

            ViewBag.ContentTypes = new SelectList(contetTypes.ToDataSource<ContentType>(), "Key", "Value");

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult Delete(string id)
        {
            var classRepository = new ClassRepository(_context);
            var uploader = new SupportMaterialUploader(id);
            var klass = classRepository.GetById(new Guid(id));

            using (var tx = new TransactionScope())
            {
                foreach (var file in klass.Files)
                {
                    uploader.DeleteFile(file.PhysicalPath);
                }

                classRepository.Delete(klass.Id);
                _context.Save(_loggedUser);

                tx.Complete();
            }
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}
