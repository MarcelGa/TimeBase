#!/usr/bin/env python3
"""Generate Python protobuf files from .proto definitions."""

import subprocess
import sys
from pathlib import Path


def generate_protos():
    """Generate Python protobuf and gRPC files."""
    # Get the SDK root directory
    sdk_root = Path(__file__).parent
    protos_dir = sdk_root / "timebase_sdk" / "protos"
    generated_dir = sdk_root / "timebase_sdk" / "generated"
    
    # Create generated directory if it doesn't exist
    generated_dir.mkdir(exist_ok=True)
    
    # Create __init__.py in generated directory
    init_file = generated_dir / "__init__.py"
    init_file.write_text('"""Generated protobuf files."""\n')
    
    # Find the proto file
    proto_file = protos_dir / "provider.proto"
    
    if not proto_file.exists():
        print(f"Error: Proto file not found: {proto_file}", file=sys.stderr)
        return 1
    
    print(f"Generating Python protobuf files from {proto_file}...")
    
    # Run protoc to generate Python files
    cmd = [
        sys.executable, "-m", "grpc_tools.protoc",
        f"--proto_path={protos_dir}",
        f"--python_out={generated_dir}",
        f"--grpc_python_out={generated_dir}",
        str(proto_file)
    ]
    
    try:
        result = subprocess.run(cmd, check=True, capture_output=True, text=True)
        print("✅ Successfully generated protobuf files!")
        print(f"   - {generated_dir / 'provider_pb2.py'}")
        print(f"   - {generated_dir / 'provider_pb2_grpc.py'}")
        return 0
    except subprocess.CalledProcessError as e:
        print(f"❌ Error generating protobuf files:", file=sys.stderr)
        print(e.stderr, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(generate_protos())
