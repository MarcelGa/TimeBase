#!/usr/bin/env python3
"""
TimeBase Provider SDK

A Python SDK for building TimeBase data providers that integrate with the TimeBase supervisor
via gRPC. Provides a clean, async interface for implementing financial data providers.
"""

from setuptools import setup, find_packages
import os

# Read README
with open("README.md", "r", encoding="utf-8") as fh:
    long_description = fh.read()

# Read requirements
with open("requirements.txt", "r", encoding="utf-8") as fh:
    requirements = [line.strip() for line in fh if line.strip() and not line.startswith("#")]

setup(
    name="timebase-sdk",
    version="1.0.0",
    author="Marcel Galovič",
    author_email="galovic.marcel@gmail.com",
    description="Python SDK for building TimeBase data providers",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/MarcelGa/TimeBase",
    packages=find_packages(),
    classifiers=[
        "Development Status :: 3 - Alpha",
        "Intended Audience :: Developers",
        "License :: OSI Approved :: MIT License",
        "Operating System :: OS Independent",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
        "Topic :: Software Development :: Libraries",
        "Topic :: Office/Business :: Financial",
    ],
    python_requires=">=3.11",
    install_requires=requirements,
    extras_require={
        "dev": [
            "pytest>=7.0.0",
            "pytest-asyncio>=0.21.0",
            "black>=23.0.0",
            "isort>=5.12.0",
            "mypy>=1.0.0",
            "flake8>=6.0.0",
        ],
    },
    entry_points={
        "console_scripts": [
            "timebase-sdk=timebase_sdk.cli:main",
        ],
    },
    include_package_data=True,
    zip_safe=False,
)