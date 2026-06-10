using System;
using System.Collections.Generic;
using Microsoft.Graph;

namespace PI.Shared.O365.Extensions;

public static class IGraphServiceCollectionRequestExtensions
{
    public static async IAsyncEnumerable<User> ReadAll(this IGraphServiceUsersCollectionRequest request, Func<User, bool> filter = null)
    {
        var page = await request.GetAsync();

        while (true)
        {
            foreach (var item in page.CurrentPage)
            {
                if (filter == null || filter(item)) yield return item;
            }

            if (page.NextPageRequest == null) break;

            // this will throw exceptions when filtering by license :(
            page = await page.NextPageRequest.GetAsync();
        }
    }

    public static async IAsyncEnumerable<Event> ReadAll(this ICalendarCalendarViewCollectionRequest request)
    {
        var page = await request.GetAsync();

        while (true)
        {
            foreach (var item in page.CurrentPage)
            {
                yield return item;
            }

            if (page.NextPageRequest == null) break;

            // this will throw exceptions when filtering by license :(
            page = await page.NextPageRequest.GetAsync();
        }
    }        

    public static async IAsyncEnumerable<Event> ReadAll(this IEventInstancesCollectionRequest request)
    {
        var page = await request.GetAsync();

        while (true)
        {
            foreach (var item in page.CurrentPage)
            {
                yield return item;
            }

            if (page.NextPageRequest == null) break;

            // this will throw exceptions when filtering by license :(
            page = await page.NextPageRequest.GetAsync();
        }
    }          
}