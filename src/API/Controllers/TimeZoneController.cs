using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;

namespace Controllers
{
    [Route("/api/v1/[controller]")]
    public class TimeZoneController : APIController
    {
        private readonly IMapper _mapper;

        public TimeZoneController(
            IMapper mapper
            )
        {
            this._mapper = mapper;
        }

        [HttpGet]
        public List<Models.TimeZone> Get()
        {
            var list = new List<Models.TimeZone>();
            var timeZones = TimeZoneInfo.GetSystemTimeZones();
            foreach (var tz in timeZones)
            {
                list.Add(_mapper.Map<Models.TimeZone>(tz));
            }

            return list;
        }
    }

}
