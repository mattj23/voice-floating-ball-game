import json
import os

data_directory = "../data"


def main():
    json_files = [os.path.join(data_directory, x) for x in os.listdir(data_directory)]

    for file in json_files:
        base_name, ext = os.path.splitext(file)
        csv_name = base_name + ".csv"

        if os.path.exists(csv_name):
            continue

        # Load the file
        with open(file, "r") as handle:
            structure = json.loads(handle.read())

        header = ["Time", "Volume", "Flow"]
        bundled = []
        for element in structure:
            bundled.append([element[x] for x in header])

        bundled.sort()
        bundled = [header] + bundled

        with open(csv_name, "w") as handle:
            handle.write("\n".join(",".join(str(x) for x in line) for line in bundled))


if __name__ == '__main__':
    main()