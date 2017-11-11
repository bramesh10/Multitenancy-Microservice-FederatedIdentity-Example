﻿using AutoMapper;
using Server.Core.Time;
using Server.Service.Groups;
using Server.Service.Stops;
using Server.Service.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Server.Service;
using Web.Hubs;
using Web.Models;
using Web.Models.Dashboard;

namespace Web.Controllers
{
    [Authorize]
    public class DashboardController : LoggedInUserController
    {
        IGroupService groupService;
        IStopService stopService;

        public DashboardController(IGroupService groupService, IUserService userService, 
            IStopService stopService, TenantContext tenantContext)
            : base(userService, tenantContext)
        {
            this.groupService = groupService;
            this.stopService = stopService;
        }

        public ActionResult Index()
        {
            return View(this.getDashboardModel());
        }

        [HttpGet]
        public ActionResult Dashboard()
        {
            return PartialView(this.getDashboardModel());
        }

        [HttpPost]
        public ActionResult Stop(StopModel model)
        {
            if (ModelState.IsValid)
            {
                StopDto stop = this.stopService.Create(this.user.Id, model.GroupId, model.Reason);

                Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<GroupHub>()
                    .Clients.All.groupLifeCycleStateChange(this.tenantContext.FriendlyName, Web.Models.Group.LifeCycleState.Stopped.ToString());

                return RedirectToAction("Index", "Dashboard");
            }
            return PartialView(model);
        }

        [HttpPost]
        public ActionResult Resume(Guid stopId)
        {
            this.stopService.ProblemResolved(this.user.Id, stopId);

            Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<GroupHub>()
                .Clients.All.groupLifeCycleStateChange(this.tenantContext.FriendlyName, Web.Models.Group.LifeCycleState.Resumed.ToString());

            return RedirectToAction("Index");
        }

        private Model getDashboardModel()
        {
            Model dashboardModel = this.getUserStoppedModel();

            if (dashboardModel == null)
            {
                dashboardModel = this.getGroupsStateModel();
            }

            return dashboardModel;
        }


        private UserStoppedModel getUserStoppedModel()
        {
            UserStoppedModel modelGroupStopped = null;

            List<StopDto> unresolvedGroups = this.stopService.GetUnresolved();

            StopDto currentUserStoppedGroup = null;
            foreach (StopDto unresolvedGroup in unresolvedGroups)
            {
                if (unresolvedGroup.ById == this.user.Id)
                {
                    currentUserStoppedGroup = unresolvedGroup;
                    break;
                }
            }

            if (currentUserStoppedGroup != null)
            {
                List<GroupUserDto> users = this.groupService.GetUsers(currentUserStoppedGroup.GroupId);

                modelGroupStopped = new UserStoppedModel();
                modelGroupStopped.Group = Mapper.Map<StopDto, GroupModel>(currentUserStoppedGroup);
                modelGroupStopped.Users = Mapper.Map<List<GroupUserDto>, List<Web.Models.Group.UserModel>>(users);
            }

            return modelGroupStopped;
        }

        private GroupsStateModel getGroupsStateModel()
        {
            List<GroupDto> groups = this.groupService.GetGroups();
            List<StopDto> stops = this.stopService.GetUnresolved();

            Dictionary<Guid, StopDto> stopHash = new Dictionary<Guid, StopDto>();
            if (stops != null)
            {
                stopHash = stops.ToDictionary(x => x.GroupId);
            }

            List<GroupModel> groupsState = new List<GroupModel>();

            foreach (GroupDto group in groups)
            {
                GroupModel groupState = new GroupModel();

                groupState.Name = group.Name;
                groupState.GroupId = group.Id;
                groupState.State = Web.Models.Group.State.Working;
                groupState.CanStop = group.Users.Exists(user => user.Id == this.user.Id);

                if (stopHash.ContainsKey(group.Id))
                {
                    StopDto stop = stopHash[group.Id];
                    groupState.State = Web.Models.Group.State.Stopped;
                    groupState.StoppedBy = stop.By;
                    groupState.DownTime = TimeProvider.Current.UtcNow - stop.Date;
                    groupState.StoppedDateTime = stop.Date;
                }

                groupsState.Add(groupState);
            }

            GroupsStateModel modelGroups = new GroupsStateModel();
            modelGroups.Groups = groupsState;
            return modelGroups;
        }
    }
}