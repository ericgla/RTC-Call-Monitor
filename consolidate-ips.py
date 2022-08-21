from netaddr import *
import argparse

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="consolidate IP list")
    parser.add_argument("--input", "-i", help="Input file")

    args = parser.parse_args()
    ip_list = []

    with open(args.input, "r") as read_file:
        for line in read_file.readlines():
            ip_list.append(IPNetwork(line))

    print(f"IPs: {len(ip_list)}")
    consolidated = cidr_merge(ip_list)
    print(f"Consolidated IPs: {len(consolidated)}")
    print("Merged IP list:")
    consolidated.sort()
    for ip in consolidated:
        print(str(ip))
