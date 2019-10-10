﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace OCM.API.Common.Model.Extensions
{
    public class MediaItem
    {
        public static Model.MediaItem FromDataModel(Core.Data.MediaItem source)
        {
            if (source == null) return null;

            return new Model.MediaItem()
            {
                ID = source.Id,
                ChargePointID = source.ChargePointId,
                ItemURL = source.ItemUrl,
                ItemThumbnailURL = source.ItemThumbnailUrl,
                Comment = source.Comment,
                IsEnabled =  (bool)source.IsEnabled,
                IsVideo = source.IsVideo,
                IsFeaturedItem = source.IsFeaturedItem,
                IsExternalResource = source.IsExternalResource,
                MetadataValue = source.MetadataValue,
                DateCreated = source.DateCreated,
                User = User.BasicFromDataModel(source.User)
            };

        }
    }
}