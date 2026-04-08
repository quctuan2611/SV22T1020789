using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.HR;
using SV22T1020789.BusinessLayers;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using SV22T1020789.Admin.AppCodes;

namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Controller quản lý thông tin nhân viên và phân quyền
    /// </summary>
    [Authorize(Roles = "admin")]
    public class EmployeeController : Controller
    {
        private const string EMPLOYEE_SEARCH_INPUT = "EmployeeSearchInput";

        /// <summary>
        /// Hiển thị cấu hình tìm kiếm và phân trang nhân viên
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(EMPLOYEE_SEARCH_INPUT);
            if (input == null)
            {
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };
            }
            return View(input);
        }

        /// <summary>
        /// Truy vấn dữ liệu nhân viên dựa trên điều kiện tìm kiếm (Ajax Search)
        /// </summary>
        /// <param name="input">Điều kiện tìm kiếm và phân trang</param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            ApplicationContext.SetSessionData(EMPLOYEE_SEARCH_INPUT, input);
            var result = await HRDataService.ListEmployeesAsync(input);
            return View(result);
        }

        /// <summary>
        /// Giao diện thêm mới nhân viên
        /// </summary>
        /// <returns></returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung nhân viên";
            var model = new Employee()
            {
                EmployeeID = 0,
                IsWorking = true
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Giao diện cập nhật thông tin nhân viên
        /// </summary>
        /// <param name="id">Mã nhân viên</param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin nhân viên";
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        /// <summary>
        /// Xử lý lưu dữ liệu nhân viên (Thêm mới hoặc Cập nhật) bao gồm cả ảnh
        /// </summary>
        /// <param name="data">Thông tin nhân viên từ Form</param>
        /// <param name="uploadPhoto">File ảnh tải lên</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SaveData(Employee data, IFormFile? uploadPhoto)
        {
            try
            {
                ViewBag.Title = data.EmployeeID == 0 ? "Bổ sung nhân viên" : "Cập nhật thông tin nhân viên";

                if (string.IsNullOrWhiteSpace(data.FullName))
                    ModelState.AddModelError(nameof(data.FullName), "Vui lòng nhập họ tên nhân viên");

                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng nhập email nhân viên");
                else if (!await HRDataService.ValidateEmployeeEmailAsync(data.Email, data.EmployeeID))
                    ModelState.AddModelError(nameof(data.Email), "Email đã được sử dụng bởi nhân viên khác");

                if (!ModelState.IsValid)
                    return View("Edit", data);

                if (uploadPhoto != null)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    var filePath = Path.Combine(ApplicationContext.WWWRootPath, "images/employees", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }
                    data.Photo = fileName;
                }

                if (string.IsNullOrEmpty(data.Address)) data.Address = "";
                if (string.IsNullOrEmpty(data.Phone)) data.Phone = "";
                if (string.IsNullOrEmpty(data.Photo)) data.Photo = "nophoto.png";

                if (data.EmployeeID == 0)
                    await HRDataService.AddEmployeeAsync(data);
                else
                    await HRDataService.UpdateEmployeeAsync(data);

                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Hệ thống đang bận hoặc dữ liệu không hợp lệ.");
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xử lý xóa nhân viên (GET: Hiển thị xác nhận, POST: Thực hiện xóa)
        /// </summary>
        /// <param name="id">Mã nhân viên</param>
        /// <returns></returns>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                var emp = await HRDataService.GetEmployeeAsync(id);
                if (emp == null || emp.IsWorking) return RedirectToAction("Index");

                await HRDataService.DeleteEmployeeAsync(id);
                return RedirectToAction("Index");
            }

            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null) return RedirectToAction("Index");

            ViewBag.AllowDelete = !await HRDataService.IsUsedEmployeeAsync(id);
            return View(model);
        }

        /// <summary>
        /// Giao diện cấp lại mật khẩu cho nhân viên
        /// </summary>
        /// <param name="id">Mã nhân viên</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        /// <summary>
        /// Xử lý cập nhật mật khẩu mới (có mã hóa MD5)
        /// </summary>
        /// <param name="id">Mã nhân viên</param>
        /// <param name="newPassword">Mật khẩu mới</param>
        /// <param name="confirmPassword">Mật khẩu xác nhận</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> ChangePassword(int id, string newPassword, string confirmPassword)
        {
            var employee = await HRDataService.GetEmployeeAsync(id);
            if (employee == null) return RedirectToAction("Index");

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError("Error", "Vui lòng nhập đầy đủ mật khẩu!");
                return View(employee);
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("Error", "Mật khẩu xác nhận không khớp!");
                return View(employee);
            }

            string hashedNewPassword = CryptHelper.HashMD5(newPassword);
            bool result = SecurityDataService.ResetPassword(id.ToString(), hashedNewPassword);

            if (!result)
            {
                ModelState.AddModelError("Error", "Cập nhật mật khẩu thất bại!");
                return View(employee);
            }

            TempData["Message"] = "Đã cấp lại mật khẩu thành công!";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// GET: Hiển thị giao diện phân quyền cho nhân viên
        /// </summary>
        /// <param name="id">Mã nhân viên</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> ChangeRole(int id)
        {
            var model = await HRDataService.GetEmployeeAsync(id);
            if (model == null) return RedirectToAction("Index");

            // Lấy danh sách quyền từ cột RoleNames trong DB và chuyển thành mảng
            ViewBag.CurrentRoles = !string.IsNullOrEmpty(model.RoleNames)
                                   ? model.RoleNames.Split(',')
                                   : new string[0];

            return View(model);
        }

        /// <summary>
        /// POST: Xử lý lưu các quyền được chọn vào Database (Cột RoleNames)
        /// </summary>
        /// <param name="employeeID">Mã nhân viên</param>
        /// <param name="selectedRoles">Danh sách các mã quyền (RoleValue) được chọn từ checkbox</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SaveRole(int employeeID, List<string> selectedRoles)
        {
            var employee = await HRDataService.GetEmployeeAsync(employeeID);
            if (employee == null) return RedirectToAction("Index");

            // Nối danh sách quyền thành chuỗi cách nhau bởi dấu phẩy (Bắt chước 1020247)
            string roleNames = (selectedRoles != null && selectedRoles.Count > 0)
                               ? string.Join(",", selectedRoles)
                               : "";

            // Cập nhật và lưu vào DB
            employee.RoleNames = roleNames;
            await HRDataService.UpdateEmployeeAsync(employee);

            TempData["Message"] = "Cập nhật phân quyền nhân viên thành công!";
            return RedirectToAction("Index");
        }
    }
}