using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime.CredentialManagement;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;

namespace EC2Snapshot
{
  class Program
  {
    static void Main(string[] args)
    {
      var awsAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
      var awsSecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";

      var client = new AmazonEC2Client(
        awsAccessKeyId: awsAccessKeyId,
        awsSecretAccessKey: awsSecretAccessKey,
        region: Amazon.RegionEndpoint.APNortheast1
      );

      client.DescribeVolumesAsync().Result
        .Volumes.ForEach(volume =>
          CreateVolumeSnapshot(client, volume)
        );
    }

    static void DescribeSnapshots(AmazonEC2Client client, Volume volume)
    {
      int generation = 7;
      var describeSnapshotsReq = new DescribeSnapshotsRequest()
      {
        Filters = new List<Filter>() {
          new Filter(){
            Name = "volume-id",
            Values = new List<string>(){
              volume.VolumeId
            }
          }
        }
      };
      var resp = client.DescribeSnapshotsAsync(describeSnapshotsReq);
      var snapshots = resp.Result.Snapshots.OrderBy(i => i.StartTime).ToList();
      if (snapshots.Count >= generation)
      {
        var req = new DeleteSnapshotRequest(snapshots.First().SnapshotId);
        client.DeleteSnapshotAsync(req).Wait();
      }
    }

    static void CreateVolumeSnapshot(AmazonEC2Client client, Volume volume)
    {
      DescribeSnapshots(client, volume);

      var tag = volume.Tags.FirstOrDefault(i => i.Key == "backup");
      var backup = (tag is null) ? "off" : tag.Value;
      if (backup == "on")
      {
        var req = new CreateSnapshotRequest(volume.VolumeId, "");
        client.CreateSnapshotAsync(req).Wait();
      }
    }
  }
}
